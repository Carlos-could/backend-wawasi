namespace BackendWawasi.Auth;

public enum AppRole
{
    Member = 10,
    Admin = 20
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
            "admin" => AppRole.Admin,
            _ => AppRole.Member
        };
    }

    public static string ToWireValue(this AppRole role)
    {
        return role == AppRole.Admin ? "admin" : "member";
    }
}
