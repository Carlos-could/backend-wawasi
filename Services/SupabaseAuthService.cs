using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using BackendWawasi.Auth;
using BackendWawasi.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace BackendWawasi.Services;

public sealed class SupabaseAuthService
{
    private readonly SupabaseOptions _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _jwksConfig;

    public SupabaseAuthService(
        IOptions<SupabaseOptions> options,
        IConfigurationManager<OpenIdConnectConfiguration> jwksConfig)
    {
        _options = options.Value;
        _jwksConfig = jwksConfig;
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

        var payload = await ValidateAndParseTokenAsync(accessToken, cancellationToken);
        if (payload is null)
        {
            return new AuthorizationResult(false, StatusCodes.Status401Unauthorized, "Invalid or expired token.", null);
        }

        if (!payload.Value.Role.HasMinimumRole(minimumRole))
        {
            return new AuthorizationResult(false, StatusCodes.Status403Forbidden, "Insufficient role for this resource.", null);
        }

        var authenticatedUser = new AuthenticatedUser(payload.Value.Id, payload.Value.Email, payload.Value.Role, accessToken);
        return new AuthorizationResult(true, StatusCodes.Status200OK, null, authenticatedUser);
    }

    private async Task<(Guid Id, string? Email, AppRole Role)?> ValidateAndParseTokenAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var signingKeys = await GetSigningKeysAsync(cancellationToken);

            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                // Acepta tanto la clave asimétrica del JWKS (ES256/RS256) como el
                // secreto HS256 legacy mientras siga configurado. La validación
                // elige la clave por 'kid'/algoritmo del token.
                IssuerSigningKeys = signingKeys,
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

    private async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        var keys = new List<SecurityKey>();

        // Claves asimétricas publicadas por GoTrue (firma actual: ECC P-256).
        try
        {
            var config = await _jwksConfig.GetConfigurationAsync(cancellationToken);
            keys.AddRange(config.SigningKeys);
        }
        catch (Exception)
        {
            // Si el JWKS no está disponible no abortamos: aún podemos validar
            // tokens HS256 legacy si el secreto sigue configurado.
        }

        // Secreto HS256 legacy (opcional, en retirada).
        if (!string.IsNullOrWhiteSpace(_options.JwtSecret))
        {
            keys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret)));
        }

        return keys;
    }
}
