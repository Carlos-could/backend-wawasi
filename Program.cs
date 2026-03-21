using BackendWawasi.Configuration;
using BackendWawasi.Auth;
using BackendWawasi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddScoped<SupabaseAuthService>();
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
    var origins = allowedOrigins is { Length: > 0 } ? allowedOrigins : ["http://localhost:3000"];

    options.AddPolicy("FrontendCors", policy =>
    {
        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services
    .AddOptions<SupabaseOptions>()
    .Bind(builder.Configuration.GetSection(SupabaseOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Url) &&
                   !string.IsNullOrWhiteSpace(options.AnonKey) &&
                   !string.IsNullOrWhiteSpace(options.ServiceRoleKey),
        "Supabase configuration is missing. Set Supabase:Url, Supabase:AnonKey and Supabase:ServiceRoleKey.")
    .ValidateOnStart();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("FrontendCors");

app.MapGet("/", () => Results.Ok(new { service = "backend-wawasi", status = "up" }));
app.MapHealthChecks("/health");



app.MapGet("/api/v1/auth/me", async (
    HttpRequest request,
    SupabaseAuthService authService,
    CancellationToken cancellationToken) =>
{
    var auth = await authService.RequireMinimumRoleAsync(request, AppRole.Inquilino, cancellationToken);
    if (!auth.IsAuthorized)
    {
        return Results.Problem(auth.Error, statusCode: auth.StatusCode);
    }

    return Results.Ok(new
    {
        user = new
        {
            id = auth.User!.Id,
            email = auth.User.Email,
            role = auth.User.Role.ToWireValue()
        }
    });
});

app.MapGet("/api/v1/admin/health", async (
    HttpRequest request,
    SupabaseAuthService authService,
    CancellationToken cancellationToken) =>
{
    var auth = await authService.RequireMinimumRoleAsync(request, AppRole.Admin, cancellationToken);
    if (!auth.IsAuthorized)
    {
        return Results.Problem(auth.Error, statusCode: auth.StatusCode);
    }

    return Results.Ok(new
    {
        ok = true,
        message = "Admin endpoint enabled.",
        user = new
        {
            id = auth.User!.Id,
            email = auth.User.Email,
            role = auth.User.Role.ToWireValue()
        }
    });
});



app.Run();
