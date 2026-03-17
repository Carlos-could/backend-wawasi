using BackendWawasi.Configuration;
using BackendWawasi.Auth;
using BackendWawasi.Properties;
using BackendWawasi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddScoped<SupabaseAuthService>();
builder.Services.AddScoped<PropertiesService>();
builder.Services.AddScoped<PropertyPhotosService>();
builder.Services.AddScoped<PublicPropertiesService>();
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

app.MapGet("/api/v1/public/properties/suggestions", async (
    string? q,
    PublicPropertiesService publicPropertiesService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await publicPropertiesService.GetSuggestionsAsync(q, cancellationToken);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/v1/public/properties", async (
    [AsParameters] PublicPropertySearchQuery query,
    PublicPropertiesService publicPropertiesService,
    CancellationToken cancellationToken) =>
{
    var errors = query.Validate();
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        var result = await publicPropertiesService.SearchAsync(query, cancellationToken);
        return Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/api/v1/public/properties/{id:guid}", async (
    Guid id,
    PublicPropertiesService publicPropertiesService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await publicPropertiesService.GetByIdAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

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

app.MapPost("/api/v1/properties", async (
    HttpRequest request,
    CreatePropertyRequest payload,
    SupabaseAuthService authService,
    PropertiesService propertiesService,
    CancellationToken cancellationToken) =>
{
    var auth = await authService.RequireMinimumRoleAsync(request, AppRole.Propietario, cancellationToken);
    if (!auth.IsAuthorized)
    {
        return Results.Problem(auth.Error, statusCode: auth.StatusCode);
    }

    var errors = PropertyValidation.Validate(payload);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    PropertyResponse? created;
    try
    {
        created = await propertiesService.CreateAsync(payload, auth.User!.Id, auth.User.AccessToken, cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }

    if (created is null)
    {
        return Results.Problem("No se pudo crear la propiedad.", statusCode: StatusCodes.Status502BadGateway);
    }

    return Results.Created($"/api/v1/properties/{created.Id}", created);
});

app.MapGet("/api/v1/properties/{id:guid}", async (
    Guid id,
    HttpRequest request,
    SupabaseAuthService authService,
    PropertiesService propertiesService,
    CancellationToken cancellationToken) =>
{
    var auth = await authService.RequireMinimumRoleAsync(request, AppRole.Propietario, cancellationToken);
    if (!auth.IsAuthorized)
    {
        return Results.Problem(auth.Error, statusCode: auth.StatusCode);
    }

    PropertyResponse? property;
    try
    {
        property = await propertiesService.GetByIdAsync(id, auth.User!.AccessToken, cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }

    if (property is null)
    {
        return Results.NotFound();
    }

    if (auth.User.Role != AppRole.Admin && property.CreatedBy != auth.User.Id)
    {
        return Results.Problem("No tienes permisos para acceder a esta propiedad.", statusCode: StatusCodes.Status403Forbidden);
    }

    return Results.Ok(property);
});

app.MapPut("/api/v1/properties/{id:guid}", async (
    Guid id,
    HttpRequest request,
    UpdatePropertyRequest payload,
    SupabaseAuthService authService,
    PropertiesService propertiesService,
    CancellationToken cancellationToken) =>
{
    var auth = await authService.RequireMinimumRoleAsync(request, AppRole.Propietario, cancellationToken);
    if (!auth.IsAuthorized)
    {
        return Results.Problem(auth.Error, statusCode: auth.StatusCode);
    }

    var errors = PropertyValidation.Validate(payload);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    PropertyResponse? existing;
    try
    {
        existing = await propertiesService.GetByIdAsync(id, auth.User!.AccessToken, cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }

    if (existing is null)
    {
        return Results.NotFound();
    }

    if (auth.User.Role != AppRole.Admin && existing.CreatedBy != auth.User.Id)
    {
        return Results.Problem("No tienes permisos para editar esta propiedad.", statusCode: StatusCodes.Status403Forbidden);
    }

    PropertyResponse? updated;
    try
    {
        updated = await propertiesService.UpdateAsync(id, payload, auth.User.AccessToken, cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }

    if (updated is null)
    {
        return Results.Problem("No se pudo actualizar la propiedad.", statusCode: StatusCodes.Status502BadGateway);
    }

    return Results.Ok(updated);
});

app.MapGet("/api/v1/properties/{id:guid}/photos", async (
    Guid id,
    HttpRequest request,
    SupabaseAuthService authService,
    PropertiesService propertiesService,
    PropertyPhotosService photosService,
    CancellationToken cancellationToken) =>
{
    var auth = await authService.RequireMinimumRoleAsync(request, AppRole.Propietario, cancellationToken);
    if (!auth.IsAuthorized)
    {
        return Results.Problem(auth.Error, statusCode: auth.StatusCode);
    }

    PropertyResponse? property;
    try
    {
        property = await propertiesService.GetByIdAsync(id, auth.User!.AccessToken, cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }

    if (property is null)
    {
        return Results.NotFound();
    }

    if (auth.User.Role != AppRole.Admin && property.CreatedBy != auth.User.Id)
    {
        return Results.Problem("No tienes permisos para acceder a las fotos de esta propiedad.", statusCode: StatusCodes.Status403Forbidden);
    }

    try
    {
        var photos = await photosService.ListAsync(id, auth.User.AccessToken, cancellationToken);
        return Results.Ok(photos);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/api/v1/properties/{id:guid}/photos", async (
    Guid id,
    HttpRequest request,
    SupabaseAuthService authService,
    PropertiesService propertiesService,
    PropertyPhotosService photosService,
    CancellationToken cancellationToken) =>
{
    var auth = await authService.RequireMinimumRoleAsync(request, AppRole.Propietario, cancellationToken);
    if (!auth.IsAuthorized)
    {
        return Results.Problem(auth.Error, statusCode: auth.StatusCode);
    }

    PropertyResponse? property;
    try
    {
        property = await propertiesService.GetByIdAsync(id, auth.User!.AccessToken, cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }

    if (property is null)
    {
        return Results.NotFound();
    }

    if (auth.User.Role != AppRole.Admin && property.CreatedBy != auth.User.Id)
    {
        return Results.Problem("No tienes permisos para subir fotos en esta propiedad.", statusCode: StatusCodes.Status403Forbidden);
    }

    IFormCollection form;
    try
    {
        form = await request.ReadFormAsync(cancellationToken);
    }
    catch (InvalidDataException)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["files"] = ["No se pudo leer el formulario de subida."]
        });
    }

    var files = form.Files.GetFiles("files[]");
    if (files.Count == 0 && form.Files.Count > 0)
    {
        files = [.. form.Files];
    }

    try
    {
        var photos = await photosService.UploadAsync(id, files, auth.User.AccessToken, cancellationToken);
        return Results.Ok(photos);
    }
    catch (ArgumentException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["files"] = [ex.Message]
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
}).DisableAntiforgery();

app.MapPatch("/api/v1/properties/{id:guid}/photos/order", async (
    Guid id,
    HttpRequest request,
    ReorderPropertyPhotosRequest payload,
    SupabaseAuthService authService,
    PropertiesService propertiesService,
    PropertyPhotosService photosService,
    CancellationToken cancellationToken) =>
{
    var auth = await authService.RequireMinimumRoleAsync(request, AppRole.Propietario, cancellationToken);
    if (!auth.IsAuthorized)
    {
        return Results.Problem(auth.Error, statusCode: auth.StatusCode);
    }

    PropertyResponse? property;
    try
    {
        property = await propertiesService.GetByIdAsync(id, auth.User!.AccessToken, cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }

    if (property is null)
    {
        return Results.NotFound();
    }

    if (auth.User.Role != AppRole.Admin && property.CreatedBy != auth.User.Id)
    {
        return Results.Problem("No tienes permisos para reordenar fotos en esta propiedad.", statusCode: StatusCodes.Status403Forbidden);
    }

    try
    {
        var photos = await photosService.ReorderAsync(id, payload.PhotoIds, auth.User.AccessToken, cancellationToken);
        return Results.Ok(photos);
    }
    catch (ArgumentException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(payload.PhotoIds)] = [ex.Message]
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPatch("/api/v1/properties/{id:guid}/photos/{photoId:guid}/primary", async (
    Guid id,
    Guid photoId,
    HttpRequest request,
    SupabaseAuthService authService,
    PropertiesService propertiesService,
    PropertyPhotosService photosService,
    CancellationToken cancellationToken) =>
{
    var auth = await authService.RequireMinimumRoleAsync(request, AppRole.Propietario, cancellationToken);
    if (!auth.IsAuthorized)
    {
        return Results.Problem(auth.Error, statusCode: auth.StatusCode);
    }

    PropertyResponse? property;
    try
    {
        property = await propertiesService.GetByIdAsync(id, auth.User!.AccessToken, cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }

    if (property is null)
    {
        return Results.NotFound();
    }

    if (auth.User.Role != AppRole.Admin && property.CreatedBy != auth.User.Id)
    {
        return Results.Problem("No tienes permisos para marcar principal en esta propiedad.", statusCode: StatusCodes.Status403Forbidden);
    }

    try
    {
        var photos = await photosService.SetPrimaryAsync(id, photoId, auth.User.AccessToken, cancellationToken);
        return Results.Ok(photos);
    }
    catch (ArgumentException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["photoId"] = [ex.Message]
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapDelete("/api/v1/properties/{id:guid}/photos/{photoId:guid}", async (
    Guid id,
    Guid photoId,
    HttpRequest request,
    SupabaseAuthService authService,
    PropertiesService propertiesService,
    PropertyPhotosService photosService,
    CancellationToken cancellationToken) =>
{
    var auth = await authService.RequireMinimumRoleAsync(request, AppRole.Propietario, cancellationToken);
    if (!auth.IsAuthorized)
    {
        return Results.Problem(auth.Error, statusCode: auth.StatusCode);
    }

    PropertyResponse? property;
    try
    {
        property = await propertiesService.GetByIdAsync(id, auth.User!.AccessToken, cancellationToken);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }

    if (property is null)
    {
        return Results.NotFound();
    }

    if (auth.User.Role != AppRole.Admin && property.CreatedBy != auth.User.Id)
    {
        return Results.Problem("No tienes permisos para eliminar fotos en esta propiedad.", statusCode: StatusCodes.Status403Forbidden);
    }

    try
    {
        var photos = await photosService.DeleteAsync(id, photoId, auth.User.AccessToken, cancellationToken);
        return Results.Ok(photos);
    }
    catch (ArgumentException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["photoId"] = [ex.Message]
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();
