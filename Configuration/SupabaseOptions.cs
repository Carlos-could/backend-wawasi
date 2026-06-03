namespace BackendWawasi.Configuration;

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    public string Url { get; init; } = string.Empty;

    public string AnonKey { get; init; } = string.Empty;

    public string ServiceRoleKey { get; init; } = string.Empty;

    /// <summary>
    /// Secreto HS256 legacy de Supabase. Opcional: solo necesario mientras existan
    /// tokens firmados con la clave legacy. Los tokens nuevos se validan vía JWKS (ECC/RSA).
    /// </summary>
    public string JwtSecret { get; init; } = string.Empty;

    /// <summary>
    /// Endpoint JWKS de GoTrue. Si está vacío se deriva de <see cref="Url"/>:
    /// <c>{Url}/auth/v1/.well-known/jwks.json</c>.
    /// </summary>
    public string JwksUrl { get; init; } = string.Empty;

    public string ResolvedJwksUrl =>
        string.IsNullOrWhiteSpace(JwksUrl)
            ? $"{Url.TrimEnd('/')}/auth/v1/.well-known/jwks.json"
            : JwksUrl;
}
