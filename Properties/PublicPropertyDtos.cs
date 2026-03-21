namespace BackendWawasi.Properties;

public sealed class PublicPropertySuggestionsResponse
{
    public List<PublicPropertySuggestionItem> Items { get; init; } = [];
}

public sealed class PublicPropertySuggestionItem
{
    public string Value { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}

public sealed class PublicPropertySearchQuery
{
    public string? Q { get; init; }
    public string? CityPostal { get; init; }
    public decimal? PriceMax { get; init; }
    public short? Bedrooms { get; init; }
    public decimal? AreaMin { get; init; }
    public DateOnly? AvailableFrom { get; init; }
    public string? Sort { get; init; }
    public int? Offset { get; init; }
    public int? Limit { get; init; }

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        static void AddError(Dictionary<string, string[]> dictionary, string key, string message)
        {
            if (dictionary.TryGetValue(key, out var existing))
            {
                dictionary[key] = [.. existing, message];
                return;
            }

            dictionary[key] = [message];
        }

        if (PriceMax is not null && PriceMax < 0)
            AddError(errors, nameof(PriceMax), "priceMax no puede ser negativo.");

        if (Bedrooms is not null && Bedrooms < 0)
            AddError(errors, nameof(Bedrooms), "bedrooms no puede ser negativo.");

        if (AreaMin is not null && AreaMin <= 0)
            AddError(errors, nameof(AreaMin), "areaMin debe ser mayor a 0.");

        if (Offset is not null && Offset < 0)
            AddError(errors, nameof(Offset), "offset no puede ser negativo.");

        if (Limit is not null && (Limit < 1 || Limit > 50))
            AddError(errors, nameof(Limit), "limit debe estar entre 1 y 50.");

        var sort = Sort?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(sort) && sort is not ("recent" or "price_asc" or "price_desc"))
            AddError(errors, nameof(Sort), "sort invalido. Usa recent, price_asc o price_desc.");

        return errors;
    }

    public int ResolveOffset() => Offset.GetValueOrDefault(0);
    public int ResolveLimit() => Math.Clamp(Limit.GetValueOrDefault(20), 1, 50);
    public string ResolveSort() => Sort?.Trim().ToLowerInvariant() switch
    {
        "price_asc" => "price_asc",
        "price_desc" => "price_desc",
        _ => "recent"
    };
}

public sealed class PublicPropertyListResponse
{
    public List<PublicPropertyListItem> Items { get; init; } = [];
    public int Total { get; init; }
    public int Offset { get; init; }
    public int Limit { get; init; }
}

public sealed class PublicPropertyListItem
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string? Zone { get; init; }
    public decimal? AreaM2 { get; init; }
    public short? Bedrooms { get; init; }
    public string? ThumbnailUrl { get; init; }
    public PublicPropertyLocation? Location { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class PublicPropertyLocation
{
    public decimal Lat { get; init; }
    public decimal Lng { get; init; }
    public string Precision { get; init; } = "approximate";
}

public sealed class PublicPropertyImageItem
{
    public string Url { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public int SortOrder { get; init; }
}

public sealed class PublicPropertyDetailResponse
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public string PropertyType { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string? Zone { get; init; }
    public string Country { get; init; } = string.Empty;
    public string? PostalCode { get; init; }
    public short? Bedrooms { get; init; }
    public short? Bathrooms { get; init; }
    public decimal? AreaM2 { get; init; }
    public DateOnly? AvailableFrom { get; init; }
    public decimal? Kaltmiete { get; init; }
    public decimal? Nebenkosten { get; init; }
    public decimal? Warmmiete { get; init; }
    public decimal? Kaution { get; init; }
    public PublicPropertyLocation? Location { get; init; }
    public string LocationPrecisionLabel { get; init; } = string.Empty;
    public List<PublicPropertyImageItem> Images { get; init; } = [];
}
