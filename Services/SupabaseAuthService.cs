using System.Net.Http.Headers;
using System.Text.Json;
using BackendWawasi.Auth;
using BackendWawasi.Configuration;
using Microsoft.Extensions.Options;

namespace BackendWawasi.Services;

public sealed class SupabaseAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SupabaseOptions _options;

    public SupabaseAuthService(
        IHttpClientFactory httpClientFactory,
        IOptions<SupabaseOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<AuthorizationResult> RequireMinimumRoleAsync(
        HttpRequest request,
        AppRole minimumRole,
        CancellationToken cancellationToken = default)
    {
        var authHeader = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return new AuthorizationResult(false, StatusCodes.Status401Unauthorized, "Missing bearer token.", null);
        }

        var accessToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new AuthorizationResult(false, StatusCodes.Status401Unauthorized, "Invalid bearer token.", null);
        }

        var user = await GetUserFromAccessTokenAsync(accessToken, cancellationToken);
        if (user is null)
        {
            return new AuthorizationResult(false, StatusCodes.Status401Unauthorized, "Invalid or expired token.", null);
        }

        var userValue = user.Value;
        var role = await GetProfileRoleAsync(userValue.Id, cancellationToken);
        if (role is null)
        {
            return new AuthorizationResult(false, StatusCodes.Status403Forbidden, "User profile not found or inaccessible.", null);
        }

        if (!role.Value.HasMinimumRole(minimumRole))
        {
            return new AuthorizationResult(false, StatusCodes.Status403Forbidden, "Insufficient role for this resource.", null);
        }

        var authenticatedUser = new AuthenticatedUser(userValue.Id, userValue.Email, role.Value, accessToken);
        return new AuthorizationResult(true, StatusCodes.Status200OK, null, authenticatedUser);
    }

    private async Task<(Guid Id, string? Email)?> GetUserFromAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.Url.TrimEnd('/')}/auth/v1/user");
        request.Headers.Add("apikey", _options.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = json.RootElement;
        if (!root.TryGetProperty("id", out var idNode))
        {
            return null;
        }

        if (!Guid.TryParse(idNode.GetString(), out var userId))
        {
            return null;
        }

        var email = root.TryGetProperty("email", out var emailNode) ? emailNode.GetString() : null;
        return (userId, email);
    }

    private async Task<AppRole?> GetProfileRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var url = $"{_options.Url.TrimEnd('/')}/rest/v1/profiles?user_id=eq.{userId}&select=role&limit=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("apikey", _options.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var first = json.RootElement[0];
        if (!first.TryGetProperty("role", out var roleNode))
        {
            return null;
        }

        return AppRoleExtensions.ParseOrDefault(roleNode.GetString());
    }
}
