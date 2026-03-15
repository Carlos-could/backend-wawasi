using BackendWawasi.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

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

app.MapGet("/", () => Results.Ok(new { service = "backend-wawasi", status = "up" }));
app.MapHealthChecks("/health");

app.Run();
