using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BackendWawasi.Configuration;
using Microsoft.Extensions.Options;

namespace BackendWawasi.Properties;

public sealed class PropertiesService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SupabaseOptions _supabaseOptions;

    public PropertiesService(
        IHttpClientFactory httpClientFactory,
        IOptions<SupabaseOptions> supabaseOptions)
    {
        _httpClientFactory = httpClientFactory;
        _supabaseOptions = supabaseOptions.Value;
    }

    public async Task<PropertyResponse?> GetByIdAsync(Guid id, string accessToken, CancellationToken cancellationToken)
    {
        var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/properties?id=eq.{id}&select=*";
        var rows = await SendArrayRequestAsync(HttpMethod.Get, endpoint, accessToken, null, cancellationToken);
        return rows.Count == 0 ? null : rows[0].ToResponse();
    }

    public async Task<PropertyResponse?> CreateAsync(CreatePropertyRequest request, Guid createdBy, string accessToken, CancellationToken cancellationToken)
    {
        var payload = new PropertyWritePayload(request, createdBy);
        var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/properties?select=*";
        var rows = await SendArrayRequestAsync(HttpMethod.Post, endpoint, accessToken, payload, cancellationToken);
        return rows.Count == 0 ? null : rows[0].ToResponse();
    }

    public async Task<PropertyResponse?> UpdateAsync(Guid id, UpdatePropertyRequest request, string accessToken, CancellationToken cancellationToken)
    {
        var payload = new PropertyWritePayload(request, null);
        var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/properties?id=eq.{id}&select=*";
        var rows = await SendArrayRequestAsync(new HttpMethod("PATCH"), endpoint, accessToken, payload, cancellationToken);
        return rows.Count == 0 ? null : rows[0].ToResponse();
    }

    private async Task<List<PropertyRow>> SendArrayRequestAsync(HttpMethod method, string url, string accessToken, object? body, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("apikey", _supabaseOptions.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Prefer", "return=representation");

        if (body is not null)
        {
            var jsonBody = JsonSerializer.Serialize(body, SerializerOptions);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Supabase request failed ({(int)response.StatusCode}): {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<List<PropertyRow>>(stream, cancellationToken: cancellationToken);
        return result ?? [];
    }

    private sealed class PropertyWritePayload
    {
        public PropertyWritePayload(CreatePropertyRequest request, Guid? createdBy)
        {
            CreatedBy = createdBy;
            Title = request.Title.Trim();
            Description = request.Description?.Trim();
            PropertyType = request.PropertyType;
            OperationType = request.OperationType;
            Status = request.Status;
            Price = request.Price;
            Currency = request.Currency.Trim().ToUpperInvariant();
            Bedrooms = request.Bedrooms;
            Bathrooms = request.Bathrooms;
            AreaM2 = request.AreaM2;
            AddressLine = request.AddressLine?.Trim();
            District = request.District?.Trim();
            City = request.City.Trim();
            Country = request.Country.Trim();
            PostalCode = request.PostalCode?.Trim();
            Kaltmiete = request.Kaltmiete;
            Nebenkosten = request.Nebenkosten;
            Warmmiete = request.Warmmiete;
            Kaution = request.Kaution;
        }

        [JsonPropertyName("created_by")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? CreatedBy { get; }

        [JsonPropertyName("title")]
        public string Title { get; }

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; }

        [JsonPropertyName("property_type")]
        public string PropertyType { get; }

        [JsonPropertyName("operation_type")]
        public string OperationType { get; }

        [JsonPropertyName("status")]
        public string Status { get; }

        [JsonPropertyName("price")]
        public decimal Price { get; }

        [JsonPropertyName("currency")]
        public string Currency { get; }

        [JsonPropertyName("bedrooms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public short? Bedrooms { get; }

        [JsonPropertyName("bathrooms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public short? Bathrooms { get; }

        [JsonPropertyName("area_m2")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? AreaM2 { get; }

        [JsonPropertyName("address_line")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AddressLine { get; }

        [JsonPropertyName("district")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? District { get; }

        [JsonPropertyName("city")]
        public string City { get; }

        [JsonPropertyName("country")]
        public string Country { get; }

        [JsonPropertyName("postal_code")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PostalCode { get; }

        [JsonPropertyName("kaltmiete")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Kaltmiete { get; }

        [JsonPropertyName("nebenkosten")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Nebenkosten { get; }

        [JsonPropertyName("warmmiete")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Warmmiete { get; }

        [JsonPropertyName("kaution")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public decimal? Kaution { get; }
    }

    private sealed class PropertyRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; init; }

        [JsonPropertyName("created_by")]
        public Guid CreatedBy { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("property_type")]
        public string PropertyType { get; init; } = string.Empty;

        [JsonPropertyName("operation_type")]
        public string OperationType { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; init; }

        [JsonPropertyName("currency")]
        public string Currency { get; init; } = string.Empty;

        [JsonPropertyName("bedrooms")]
        public short? Bedrooms { get; init; }

        [JsonPropertyName("bathrooms")]
        public short? Bathrooms { get; init; }

        [JsonPropertyName("area_m2")]
        public decimal? AreaM2 { get; init; }

        [JsonPropertyName("address_line")]
        public string? AddressLine { get; init; }

        [JsonPropertyName("district")]
        public string? District { get; init; }

        [JsonPropertyName("city")]
        public string City { get; init; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; init; } = string.Empty;

        [JsonPropertyName("postal_code")]
        public string? PostalCode { get; init; }

        [JsonPropertyName("kaltmiete")]
        public decimal? Kaltmiete { get; init; }

        [JsonPropertyName("nebenkosten")]
        public decimal? Nebenkosten { get; init; }

        [JsonPropertyName("warmmiete")]
        public decimal? Warmmiete { get; init; }

        [JsonPropertyName("kaution")]
        public decimal? Kaution { get; init; }

        public PropertyResponse ToResponse()
        {
            return new PropertyResponse
            {
                Id = Id,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt,
                CreatedBy = CreatedBy,
                Title = Title,
                Description = Description,
                PropertyType = PropertyType,
                OperationType = OperationType,
                Status = Status,
                Price = Price,
                Currency = Currency,
                Bedrooms = Bedrooms,
                Bathrooms = Bathrooms,
                AreaM2 = AreaM2,
                AddressLine = AddressLine,
                District = District,
                City = City,
                Country = Country,
                PostalCode = PostalCode,
                Kaltmiete = Kaltmiete,
                Nebenkosten = Nebenkosten,
                Warmmiete = Warmmiete,
                Kaution = Kaution
            };
        }
    }
}
