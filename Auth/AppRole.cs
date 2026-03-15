namespace BackendWawasi.Auth;

public enum AppRole
{
    Inquilino = 10,
    Propietario = 20,
    Admin = 30
}

public static class AppRoleExtensions
{
    public static bool HasMinimumRole(this AppRole current, AppRole minimum)
    {
        return (int)current >= (int)minimum;
    }

    public static AppRole ParseOrDefault(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "propietario" => AppRole.Propietario,
            "member" => AppRole.Propietario,
            "admin" => AppRole.Admin,
            _ => AppRole.Inquilino
        };
    }

    public static string ToWireValue(this AppRole role)
    {
        return role switch
        {
            AppRole.Admin => "admin",
            AppRole.Propietario => "propietario",
            _ => "inquilino"
        };
    }
}
