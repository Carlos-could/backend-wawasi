namespace BackendWawasi.Properties;

public sealed class PropertyPhotoResponse
{
    public Guid Id { get; init; }
    public Guid PropertyId { get; init; }
    public string StoragePath { get; init; } = string.Empty;
    public string? PublicUrl { get; init; }
    public int SortOrder { get; init; }
    public bool IsPrimary { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class ReorderPropertyPhotosRequest
{
    public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
}
