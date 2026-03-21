using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using BackendWawasi.Configuration;
using Microsoft.Extensions.Options;

namespace BackendWawasi.Properties;

public sealed class PublicPropertiesService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SupabaseOptions _supabaseOptions;

    public PublicPropertiesService(
        IHttpClientFactory httpClientFactory,
        IOptions<SupabaseOptions> supabaseOptions)
    {
        _httpClientFactory = httpClientFactory;
        _supabaseOptions = supabaseOptions.Value;
    }

    public async Task<PublicPropertySuggestionsResponse> GetSuggestionsAsync(string? rawQuery, CancellationToken cancellationToken)
    {
        var query = rawQuery?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new PublicPropertySuggestionsResponse();
        }

        var wildcard = Uri.EscapeDataString($"*{EscapeLike(query)}*");
        var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/properties?status=eq.published&or=(city.ilike.{wildcard},postal_code.ilike.{wildcard},zone.ilike.{wildcard})&select=city,postal_code,zone&limit=50";
        var rows = await SendArrayRequestAsync<SuggestionRow>(endpoint, includeCount: false, cancellationToken);

        var items = new List<(PublicPropertySuggestionItem item, int score)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            TryAddSuggestion(row.City, "city");
            TryAddSuggestion(row.PostalCode, "postal_code");
            TryAddSuggestion(row.Zone, "zone");
        }

        var top = items
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.item.Value, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.item)
            .Take(5)
            .ToList();

        return new PublicPropertySuggestionsResponse { Items = top };

        void TryAddSuggestion(string? value, string type)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var normalized = value.Trim();
            var key = $"{type}:{normalized}";
            if (!seen.Add(key))
                return;

            var score = RankSuggestion(normalized, query);
            items.Add((new PublicPropertySuggestionItem
            {
                Value = normalized,
                Type = type
            }, score));
        }
    }

    public async Task<PublicPropertyListResponse> SearchAsync(PublicPropertySearchQuery query, CancellationToken cancellationToken)
    {
        var filters = new List<string> { "status=eq.published" };
        var orTerms = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var wildcard = Uri.EscapeDataString($"*{EscapeLike(query.Q.Trim())}*");
            orTerms.Add($"city.ilike.{wildcard}");
            orTerms.Add($"postal_code.ilike.{wildcard}");
            orTerms.Add($"zone.ilike.{wildcard}");
        }

        if (!string.IsNullOrWhiteSpace(query.CityPostal))
        {
            var wildcard = Uri.EscapeDataString($"*{EscapeLike(query.CityPostal.Trim())}*");
            orTerms.Add($"city.ilike.{wildcard}");
            orTerms.Add($"postal_code.ilike.{wildcard}");
        }

        if (orTerms.Count > 0)
            filters.Add($"or=({string.Join(",", orTerms.Distinct(StringComparer.Ordinal))})");

        if (query.PriceMax is not null)
            filters.Add($"price=lte.{query.PriceMax.Value.ToString(CultureInfo.InvariantCulture)}");

        if (query.Bedrooms is not null)
            filters.Add($"bedrooms=gte.{query.Bedrooms.Value}");

        if (query.AreaMin is not null)
            filters.Add($"area_m2=gte.{query.AreaMin.Value.ToString(CultureInfo.InvariantCulture)}");

        if (query.AvailableFrom is not null)
            filters.Add($"available_from=lte.{query.AvailableFrom.Value:yyyy-MM-dd}");

        var sort = query.ResolveSort() switch
        {
            "price_asc" => "price.asc,created_at.desc",
            "price_desc" => "price.desc,created_at.desc",
            _ => "created_at.desc"
        };

        var offset = query.ResolveOffset();
        var limit = query.ResolveLimit();
        var end = offset + limit - 1;
        var select = "id,title,price,currency,city,zone,area_m2,bedrooms,lat,lng,location_precision,created_at,property_images(public_url,is_primary,sort_order)";
        var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/properties?{string.Join("&", filters)}&select={select}&order={Uri.EscapeDataString(sort)}&offset={offset}&limit={limit}";

        var (rows, total) = await SendArrayRequestWithCountAsync<PropertyListRow>(endpoint, cancellationToken, offset, end);

        return new PublicPropertyListResponse
        {
            Items = rows.Select(MapItem).ToList(),
            Total = total,
            Offset = offset,
            Limit = limit
        };
    }

    public async Task<PublicPropertyDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/properties?id=eq.{id}&status=eq.published&select=id,title,description,price,currency,operation_type,property_type,city,zone,country,postal_code,bedrooms,bathrooms,area_m2,available_from,kaltmiete,nebenkosten,warmmiete,kaution,lat,lng,location_precision,property_images(public_url,is_primary,sort_order)";
        
        var rows = await SendArrayRequestAsync<PropertyDetailRow>(endpoint, includeCount: false, cancellationToken);
        var row = rows.FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        return MapDetailItem(row);
    }


    private PublicPropertyListItem MapItem(PropertyListRow row)
    {
        var thumbnail = row.PropertyImages?
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SortOrder)
            .Select(x => x.PublicUrl)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var location = row.Lat is not null && row.Lng is not null
            ? new PublicPropertyLocation
            {
                Lat = row.Lat.Value,
                Lng = row.Lng.Value,
                Precision = row.LocationPrecision is "exact" or "approximate"
                    ? row.LocationPrecision
                    : "approximate"
            }
            : null;

        return new PublicPropertyListItem
        {
            Id = row.Id,
            Title = row.Title,
            Price = row.Price,
            Currency = row.Currency,
            City = row.City,
            Zone = row.Zone,
            AreaM2 = row.AreaM2,
            Bedrooms = row.Bedrooms,
            ThumbnailUrl = thumbnail,
            Location = location,
            CreatedAt = row.CreatedAt
        };
    }

    private PublicPropertyDetailResponse MapDetailItem(PropertyDetailRow row)
    {
        var precision = row.LocationPrecision is "exact" or "approximate"
            ? row.LocationPrecision
            : "approximate";

        var location = row.Lat is not null && row.Lng is not null
            ? new PublicPropertyLocation
            {
                Lat = row.Lat.Value,
                Lng = row.Lng.Value,
                Precision = precision
            }
            : null;

        var images = row.PropertyImages?
            .Where(x => !string.IsNullOrWhiteSpace(x.PublicUrl))
            .Select(x => new PublicPropertyImageItem
            {
                Url = x.PublicUrl!,
                IsPrimary = x.IsPrimary,
                SortOrder = x.SortOrder
            })
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SortOrder)
            .ToList() ?? [];

        return new PublicPropertyDetailResponse
        {
            Id = row.Id,
            Title = row.Title,
            Description = row.Description,
            Price = row.Price,
            Currency = row.Currency,
            OperationType = row.OperationType,
            PropertyType = row.PropertyType,
            City = row.City,
            Zone = row.Zone,
            Country = row.Country,
            PostalCode = row.PostalCode,
            Bedrooms = row.Bedrooms,
            Bathrooms = row.Bathrooms,
            AreaM2 = row.AreaM2,
            AvailableFrom = row.AvailableFrom,
            Kaltmiete = row.Kaltmiete,
            Nebenkosten = row.Nebenkosten,
            Warmmiete = row.Warmmiete,
            Kaution = row.Kaution,
            Location = location,
            LocationPrecisionLabel = precision == "exact" ? "Dirección exacta" : "Ubicación aproximada",
            Images = images
        };
    }

    private async Task<List<T>> SendArrayRequestAsync<T>(string url, bool includeCount, CancellationToken cancellationToken)
    {
        var (rows, _) = await SendArrayRequestWithCountAsync<T>(url, cancellationToken, 0, 0, includeCount);
        return rows;
    }

    private async Task<(List<T> Rows, int Total)> SendArrayRequestWithCountAsync<T>(
        string url,
        CancellationToken cancellationToken,
        int rangeStart,
        int rangeEnd,
        bool includeCount = true)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("apikey", _supabaseOptions.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseOptions.AnonKey);
        request.Headers.Add("Accept", "application/json");
        if (includeCount)
        {
            request.Headers.Add("Prefer", "count=exact");
            request.Headers.Add("Range-Unit", "items");
            request.Headers.TryAddWithoutValidation("Range", $"{rangeStart}-{rangeEnd}");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Supabase request failed ({(int)response.StatusCode}): {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var rows = await JsonSerializer.DeserializeAsync<List<T>>(stream, SerializerOptions, cancellationToken) ?? [];
        var total = includeCount ? ParseTotalCount(response.Headers) : rows.Count;
        return (rows, total);
    }

    private static int ParseTotalCount(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Content-Range", out IEnumerable<string>? contentRanges))
            return 0;

        var raw = contentRanges.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        var slashIndex = raw.LastIndexOf('/');
        if (slashIndex < 0)
            return 0;

        var totalPart = raw[(slashIndex + 1)..];
        return int.TryParse(totalPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var total) ? total : 0;
    }

    private static int RankSuggestion(string value, string query)
    {
        var normalizedValue = value.ToLowerInvariant();
        var normalizedQuery = query.ToLowerInvariant();
        if (normalizedValue.StartsWith(normalizedQuery, StringComparison.Ordinal))
            return 100;
        if (normalizedValue.Contains(normalizedQuery, StringComparison.Ordinal))
            return 60;
        return 10;
    }

    private static string EscapeLike(string value)
    {
        return value.Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal);
    }

    private sealed class SuggestionRow
    {
        [JsonPropertyName("city")]
        public string? City { get; init; }

        [JsonPropertyName("postal_code")]
        public string? PostalCode { get; init; }

        [JsonPropertyName("zone")]
        public string? Zone { get; init; }
    }

    private sealed class PropertyListRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("price")]
        public decimal Price { get; init; }

        [JsonPropertyName("currency")]
        public string Currency { get; init; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; init; } = string.Empty;

        [JsonPropertyName("zone")]
        public string? Zone { get; init; }

        [JsonPropertyName("area_m2")]
        public decimal? AreaM2 { get; init; }

        [JsonPropertyName("bedrooms")]
        public short? Bedrooms { get; init; }

        [JsonPropertyName("lat")]
        public decimal? Lat { get; init; }

        [JsonPropertyName("lng")]
        public decimal? Lng { get; init; }

        [JsonPropertyName("location_precision")]
        public string? LocationPrecision { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("property_images")]
        public List<PropertyImageRow>? PropertyImages { get; init; }
    }

    private sealed class PropertyDetailRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("price")]
        public decimal Price { get; init; }

        [JsonPropertyName("currency")]
        public string Currency { get; init; } = string.Empty;

        [JsonPropertyName("operation_type")]
        public string OperationType { get; init; } = string.Empty;

        [JsonPropertyName("property_type")]
        public string PropertyType { get; init; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; init; } = string.Empty;

        [JsonPropertyName("zone")]
        public string? Zone { get; init; }

        [JsonPropertyName("country")]
        public string Country { get; init; } = string.Empty;

        [JsonPropertyName("postal_code")]
        public string? PostalCode { get; init; }

        [JsonPropertyName("bedrooms")]
        public short? Bedrooms { get; init; }

        [JsonPropertyName("bathrooms")]
        public short? Bathrooms { get; init; }

        [JsonPropertyName("area_m2")]
        public decimal? AreaM2 { get; init; }

        [JsonPropertyName("available_from")]
        public DateOnly? AvailableFrom { get; init; }

        [JsonPropertyName("kaltmiete")]
        public decimal? Kaltmiete { get; init; }

        [JsonPropertyName("nebenkosten")]
        public decimal? Nebenkosten { get; init; }

        [JsonPropertyName("warmmiete")]
        public decimal? Warmmiete { get; init; }

        [JsonPropertyName("kaution")]
        public decimal? Kaution { get; init; }

        [JsonPropertyName("lat")]
        public decimal? Lat { get; init; }

        [JsonPropertyName("lng")]
        public decimal? Lng { get; init; }

        [JsonPropertyName("location_precision")]
        public string? LocationPrecision { get; init; }

        [JsonPropertyName("property_images")]
        public List<PropertyImageRow>? PropertyImages { get; init; }
    }

    private sealed class PropertyImageRow
    {
        [JsonPropertyName("public_url")]
        public string? PublicUrl { get; init; }

        [JsonPropertyName("is_primary")]
        public bool IsPrimary { get; init; }

        [JsonPropertyName("sort_order")]
        public int SortOrder { get; init; }
    }
}
