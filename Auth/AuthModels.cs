namespace BackendWawasi.Auth;

public sealed record AuthenticatedUser(
    Guid Id,
    string? Email,
    AppRole Role
);

public sealed record AuthorizationResult(
    bool IsAuthorized,
    int StatusCode,
    string? Error,
    AuthenticatedUser? User
);
