using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BackendWawasi.Configuration;
using Microsoft.Extensions.Options;

namespace BackendWawasi.Properties;

public sealed class PropertyPhotosService
{
    private const int MaxPhotosPerProperty = 15;
    private const long MaxPhotoBytes = 8L * 1024 * 1024;
    private const string BucketName = "property-images";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly Dictionary<string, string> AllowedMimeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SupabaseOptions _supabaseOptions;

    public PropertyPhotosService(
        IHttpClientFactory httpClientFactory,
        IOptions<SupabaseOptions> supabaseOptions)
    {
        _httpClientFactory = httpClientFactory;
        _supabaseOptions = supabaseOptions.Value;
    }

    public async Task<List<PropertyPhotoResponse>> ListAsync(
        Guid propertyId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/property_images?property_id=eq.{propertyId}&select=*&order=sort_order.asc";
        var rows = await SendArrayRequestAsync<PropertyPhotoRow>(HttpMethod.Get, endpoint, accessToken, null, cancellationToken);
        return rows.Select(static row => row.ToResponse()).ToList();
    }

    public async Task<List<PropertyPhotoResponse>> UploadAsync(
        Guid propertyId,
        IReadOnlyList<IFormFile> files,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            throw new ArgumentException("Debes adjuntar al menos un archivo.");
        }

        foreach (var file in files)
        {
            ValidateFile(file);
        }

        var current = await ListAsync(propertyId, accessToken, cancellationToken);
        if (current.Count + files.Count > MaxPhotosPerProperty)
        {
            throw new ArgumentException($"Solo se permiten hasta {MaxPhotosPerProperty} fotos por propiedad.");
        }

        var nextSortOrder = current.Count == 0 ? 0 : current.Max(photo => photo.SortOrder) + 1;
        var hasPrimary = current.Any(photo => photo.IsPrimary);

        foreach (var file in files)
        {
            var extension = AllowedMimeToExtension[file.ContentType];
            var photoId = Guid.NewGuid();
            var storagePath = $"properties/{propertyId}/{photoId}.{extension}";

            await UploadToStorageAsync(storagePath, file, cancellationToken);

            var payload = new PropertyImageWritePayload
            {
                Id = photoId,
                PropertyId = propertyId,
                StoragePath = storagePath,
                PublicUrl = BuildPublicUrl(storagePath),
                SortOrder = nextSortOrder,
                IsPrimary = !hasPrimary && nextSortOrder == 0
            };

            try
            {
                var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/property_images";
                await SendArrayRequestAsync<PropertyPhotoRow>(HttpMethod.Post, endpoint, accessToken, payload, cancellationToken);
            }
            catch
            {
                await DeleteFromStorageAsync(storagePath, cancellationToken);
                throw;
            }

            nextSortOrder++;
        }

        return await ListAsync(propertyId, accessToken, cancellationToken);
    }

    public async Task<List<PropertyPhotoResponse>> ReorderAsync(
        Guid propertyId,
        IReadOnlyList<Guid> photoIds,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (photoIds.Count == 0)
        {
            throw new ArgumentException("Debes enviar al menos una foto para reordenar.");
        }

        var current = await ListAsync(propertyId, accessToken, cancellationToken);
        if (current.Count == 0)
        {
            throw new ArgumentException("No hay fotos para reordenar.");
        }

        var expectedIds = current.Select(photo => photo.Id).OrderBy(id => id).ToArray();
        var receivedIds = photoIds.OrderBy(id => id).ToArray();
        if (!expectedIds.SequenceEqual(receivedIds))
        {
            throw new ArgumentException("El orden recibido debe incluir exactamente las fotos existentes.");
        }

        for (var index = 0; index < photoIds.Count; index++)
        {
            var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/property_images?id=eq.{photoIds[index]}";
            var payload = new PropertyImageOrderUpdatePayload { SortOrder = index };
            await SendArrayRequestAsync<PropertyPhotoRow>(new HttpMethod("PATCH"), endpoint, accessToken, payload, cancellationToken);
        }

        return await ListAsync(propertyId, accessToken, cancellationToken);
    }

    public async Task<List<PropertyPhotoResponse>> SetPrimaryAsync(
        Guid propertyId,
        Guid photoId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var current = await ListAsync(propertyId, accessToken, cancellationToken);
        if (!current.Any(photo => photo.Id == photoId))
        {
            throw new ArgumentException("La foto indicada no existe para esta propiedad.");
        }

        var resetEndpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/property_images?property_id=eq.{propertyId}";
        await SendArrayRequestAsync<PropertyPhotoRow>(
            new HttpMethod("PATCH"),
            resetEndpoint,
            accessToken,
            new PropertyImagePrimaryUpdatePayload { IsPrimary = false },
            cancellationToken);

        var targetEndpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/property_images?id=eq.{photoId}";
        await SendArrayRequestAsync<PropertyPhotoRow>(
            new HttpMethod("PATCH"),
            targetEndpoint,
            accessToken,
            new PropertyImagePrimaryUpdatePayload { IsPrimary = true },
            cancellationToken);

        return await ListAsync(propertyId, accessToken, cancellationToken);
    }

    public async Task<List<PropertyPhotoResponse>> DeleteAsync(
        Guid propertyId,
        Guid photoId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var current = await ListAsync(propertyId, accessToken, cancellationToken);
        var target = current.FirstOrDefault(photo => photo.Id == photoId);
        if (target is null)
        {
            throw new ArgumentException("La foto indicada no existe para esta propiedad.");
        }

        await DeleteFromStorageAsync(target.StoragePath, cancellationToken);

        var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/property_images?id=eq.{photoId}";
        await SendArrayRequestAsync<PropertyPhotoRow>(HttpMethod.Delete, endpoint, accessToken, null, cancellationToken);

        var remaining = await ListAsync(propertyId, accessToken, cancellationToken);
        if (remaining.Count == 0)
        {
            return remaining;
        }

        if (remaining.All(photo => !photo.IsPrimary))
        {
            var promoted = remaining.OrderBy(photo => photo.SortOrder).First();
            var promotedEndpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/property_images?id=eq.{promoted.Id}";
            await SendArrayRequestAsync<PropertyPhotoRow>(
                new HttpMethod("PATCH"),
                promotedEndpoint,
                accessToken,
                new PropertyImagePrimaryUpdatePayload { IsPrimary = true },
                cancellationToken);
        }

        var idsInOrder = remaining.OrderBy(photo => photo.SortOrder).Select(photo => photo.Id).ToArray();
        for (var index = 0; index < idsInOrder.Length; index++)
        {
            var reorderEndpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/rest/v1/property_images?id=eq.{idsInOrder[index]}";
            var payload = new PropertyImageOrderUpdatePayload { SortOrder = index };
            await SendArrayRequestAsync<PropertyPhotoRow>(new HttpMethod("PATCH"), reorderEndpoint, accessToken, payload, cancellationToken);
        }

        return await ListAsync(propertyId, accessToken, cancellationToken);
    }

    private static void ValidateFile(IFormFile file)
    {
        if (!AllowedMimeToExtension.ContainsKey(file.ContentType))
        {
            throw new ArgumentException("Formato invalido. Usa JPG, PNG o WEBP.");
        }

        if (file.Length <= 0)
        {
            throw new ArgumentException("El archivo no puede estar vacio.");
        }

        if (file.Length > MaxPhotoBytes)
        {
            throw new ArgumentException("El archivo supera el limite de 8MB.");
        }
    }

    private async Task UploadToStorageAsync(string storagePath, IFormFile file, CancellationToken cancellationToken)
    {
        var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/storage/v1/object/{BucketName}/{Uri.EscapeDataString(storagePath)}";
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("apikey", _supabaseOptions.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseOptions.ServiceRoleKey);
        request.Headers.Add("x-upsert", "false");

        await using var fileStream = file.OpenReadStream();
        request.Content = new StreamContent(fileStream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"No se pudo subir la foto a storage ({(int)response.StatusCode}): {errorBody}");
        }
    }

    private async Task DeleteFromStorageAsync(string storagePath, CancellationToken cancellationToken)
    {
        var endpoint = $"{_supabaseOptions.Url.TrimEnd('/')}/storage/v1/object/{BucketName}/{Uri.EscapeDataString(storagePath)}";
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        request.Headers.Add("apikey", _supabaseOptions.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _supabaseOptions.ServiceRoleKey);

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"No se pudo eliminar la foto de storage ({(int)response.StatusCode}): {errorBody}");
    }

    private async Task<List<TResponse>> SendArrayRequestAsync<TResponse>(
        HttpMethod method,
        string url,
        string accessToken,
        object? body,
        CancellationToken cancellationToken)
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
            throw new InvalidOperationException($"Supabase request failed ({(int)response.StatusCode}): {errorBody}");
        }

        if (method == HttpMethod.Delete)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<List<TResponse>>(stream, cancellationToken: cancellationToken);
        return result ?? [];
    }

    private string BuildPublicUrl(string storagePath)
    {
        return $"{_supabaseOptions.Url.TrimEnd('/')}/storage/v1/object/public/{BucketName}/{Uri.EscapeDataString(storagePath)}";
    }

    private sealed class PropertyPhotoRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("property_id")]
        public Guid PropertyId { get; init; }

        [JsonPropertyName("storage_path")]
        public string StoragePath { get; init; } = string.Empty;

        [JsonPropertyName("public_url")]
        public string? PublicUrl { get; init; }

        [JsonPropertyName("sort_order")]
        public int SortOrder { get; init; }

        [JsonPropertyName("is_primary")]
        public bool IsPrimary { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; init; }

        public PropertyPhotoResponse ToResponse()
        {
            return new PropertyPhotoResponse
            {
                Id = Id,
                PropertyId = PropertyId,
                StoragePath = StoragePath,
                PublicUrl = PublicUrl,
                SortOrder = SortOrder,
                IsPrimary = IsPrimary,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
        }
    }

    private sealed class PropertyImageWritePayload
    {
        [JsonPropertyName("id")]
        public Guid Id { get; init; }

        [JsonPropertyName("property_id")]
        public Guid PropertyId { get; init; }

        [JsonPropertyName("storage_path")]
        public string StoragePath { get; init; } = string.Empty;

        [JsonPropertyName("public_url")]
        public string PublicUrl { get; init; } = string.Empty;

        [JsonPropertyName("sort_order")]
        public int SortOrder { get; init; }

        [JsonPropertyName("is_primary")]
        public bool IsPrimary { get; init; }
    }

    private sealed class PropertyImageOrderUpdatePayload
    {
        [JsonPropertyName("sort_order")]
        public int SortOrder { get; init; }
    }

    private sealed class PropertyImagePrimaryUpdatePayload
    {
        [JsonPropertyName("is_primary")]
        public bool IsPrimary { get; init; }
    }
}
