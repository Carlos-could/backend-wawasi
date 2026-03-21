using System.ComponentModel.DataAnnotations;

namespace BackendWawasi.Properties;

public class CreatePropertyRequest
{
    [Required]
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }

    [Required]
    public string PropertyType { get; init; } = string.Empty;

    [Required]
    public string OperationType { get; init; } = string.Empty;

    [Required]
    public string Status { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; init; }

    [Required]
    public string Currency { get; init; } = "EUR";
    public short? Bedrooms { get; init; }
    public short? Bathrooms { get; init; }
    public decimal? AreaM2 { get; init; }
    public string? AddressLine { get; init; }
    public string? District { get; init; }
    public string? Zone { get; init; }

    [Required]
    public string City { get; init; } = string.Empty;

    [Required]
    public string Country { get; init; } = string.Empty;
    public string? PostalCode { get; init; }
    public DateOnly? AvailableFrom { get; init; }
    public decimal? Lat { get; init; }
    public decimal? Lng { get; init; }
    public string? LocationPrecision { get; init; }
    public decimal? Kaltmiete { get; init; }
    public decimal? Nebenkosten { get; init; }
    public decimal? Warmmiete { get; init; }
    public decimal? Kaution { get; init; }
}

public sealed class UpdatePropertyRequest : CreatePropertyRequest;

public sealed class PropertyResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public Guid CreatedBy { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string PropertyType { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Currency { get; init; } = "EUR";
    public short? Bedrooms { get; init; }
    public short? Bathrooms { get; init; }
    public decimal? AreaM2 { get; init; }
    public string? AddressLine { get; init; }
    public string? District { get; init; }
    public string? Zone { get; init; }
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string? PostalCode { get; init; }
    public DateOnly? AvailableFrom { get; init; }
    public decimal? Lat { get; init; }
    public decimal? Lng { get; init; }
    public string LocationPrecision { get; init; } = "approximate";
    public decimal? Kaltmiete { get; init; }
    public decimal? Nebenkosten { get; init; }
    public decimal? Warmmiete { get; init; }
    public decimal? Kaution { get; init; }
}
