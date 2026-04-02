using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using BackendWawasi.Auth;
using BackendWawasi.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BackendWawasi.Services;

public sealed class SupabaseAuthService
{
    private readonly SupabaseOptions _options;

    public SupabaseAuthService(
        IOptions<SupabaseOptions> options)
    {
        _options = options.Value;
    }

    public Task<AuthorizationResult> RequireMinimumRoleAsync(
        HttpRequest request,
        AppRole minimumRole,
        CancellationToken cancellationToken = default)
    {
        var authHeader = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new AuthorizationResult(false, StatusCodes.Status401Unauthorized, "Missing bearer token.", null));
        }

        var accessToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Task.FromResult(new AuthorizationResult(false, StatusCodes.Status401Unauthorized, "Invalid bearer token.", null));
        }

        var payload = ValidateAndParseToken(accessToken);
        if (payload is null)
        {
            return Task.FromResult(new AuthorizationResult(false, StatusCodes.Status401Unauthorized, "Invalid or expired token.", null));
        }

        if (!payload.Value.Role.HasMinimumRole(minimumRole))
        {
            return Task.FromResult(new AuthorizationResult(false, StatusCodes.Status403Forbidden, "Insufficient role for this resource.", null));
        }

        var authenticatedUser = new AuthenticatedUser(payload.Value.Id, payload.Value.Email, payload.Value.Role, accessToken);
        return Task.FromResult(new AuthorizationResult(true, StatusCodes.Status200OK, null, authenticatedUser));
    }

    private (Guid Id, string? Email, AppRole Role)? ValidateAndParseToken(string accessToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_options.JwtSecret);

            tokenHandler.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            
            var userIdString = jwtToken.Subject ?? jwtToken.Claims.FirstOrDefault(x => x.Type == "sub")?.Value;
            if (!Guid.TryParse(userIdString, out var userId)) return null;

            var email = jwtToken.Claims.FirstOrDefault(x => x.Type == "email")?.Value;

            var appMetadataJson = jwtToken.Claims.FirstOrDefault(x => x.Type == "app_metadata")?.Value;
            AppRole role = AppRole.Inquilino; // default si no se especifica rol superior
            
            if (!string.IsNullOrWhiteSpace(appMetadataJson))
            {
                using var jsonDoc = JsonDocument.Parse(appMetadataJson);
                if (jsonDoc.RootElement.TryGetProperty("role", out var roleElement))
                {
                    var roleStr = roleElement.GetString();
                    role = AppRoleExtensions.ParseOrDefault(roleStr);
                }
            }
            
            return (userId, email, role);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
