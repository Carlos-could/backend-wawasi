namespace BackendWawasi.Properties;

public static class PropertyValidation
{
    private static readonly HashSet<string> AllowedPropertyTypes =
    [
        "apartment", "house", "studio", "land", "commercial", "other"
    ];

    private static readonly HashSet<string> AllowedOperationTypes =
    [
        "rent", "sale"
    ];

    private static readonly HashSet<string> AllowedStatuses =
    [
        "draft", "published"
    ];

    public static Dictionary<string, string[]> Validate(CreatePropertyRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        static void AddError(Dictionary<string, string[]> dictionary, string field, string error)
        {
            if (dictionary.TryGetValue(field, out var existing))
            {
                dictionary[field] = [.. existing, error];
                return;
            }

            dictionary[field] = [error];
        }

        if (string.IsNullOrWhiteSpace(request.Title))
            AddError(errors, nameof(request.Title), "El titulo es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.City))
            AddError(errors, nameof(request.City), "La ciudad es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.Country))
            AddError(errors, nameof(request.Country), "El pais es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3)
            AddError(errors, nameof(request.Currency), "La moneda debe tener 3 caracteres.");

        if (!AllowedPropertyTypes.Contains(request.PropertyType))
            AddError(errors, nameof(request.PropertyType), "Tipo de propiedad invalido.");
        if (!AllowedOperationTypes.Contains(request.OperationType))
            AddError(errors, nameof(request.OperationType), "Tipo de operacion invalido.");
        if (!AllowedStatuses.Contains(request.Status))
            AddError(errors, nameof(request.Status), "Estado invalido. Usa draft o published.");

        if (request.Price < 0)
            AddError(errors, nameof(request.Price), "El precio no puede ser negativo.");
        if (request.Bedrooms is not null && request.Bedrooms < 0)
            AddError(errors, nameof(request.Bedrooms), "Dormitorios no puede ser negativo.");
        if (request.Bathrooms is not null && request.Bathrooms < 0)
            AddError(errors, nameof(request.Bathrooms), "Banos no puede ser negativo.");
        if (request.AreaM2 is not null && request.AreaM2 <= 0)
            AddError(errors, nameof(request.AreaM2), "El area debe ser mayor a 0.");
        if (request.Kaltmiete is not null && request.Kaltmiete < 0)
            AddError(errors, nameof(request.Kaltmiete), "Kaltmiete no puede ser negativo.");
        if (request.Nebenkosten is not null && request.Nebenkosten < 0)
            AddError(errors, nameof(request.Nebenkosten), "Nebenkosten no puede ser negativo.");
        if (request.Warmmiete is not null && request.Warmmiete < 0)
            AddError(errors, nameof(request.Warmmiete), "Warmmiete no puede ser negativo.");
        if (request.Kaution is not null && request.Kaution < 0)
            AddError(errors, nameof(request.Kaution), "Kaution no puede ser negativo.");

        if (request.Warmmiete is not null && request.Kaltmiete is not null && request.Warmmiete < request.Kaltmiete)
            AddError(errors, nameof(request.Warmmiete), "Warmmiete debe ser mayor o igual a Kaltmiete.");

        return errors;
    }
}
