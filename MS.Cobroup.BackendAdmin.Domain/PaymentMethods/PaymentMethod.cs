namespace MS.Cobroup.BackendAdmin.Domain.PaymentMethods;

public sealed class PaymentMethod
{
    private PaymentMethod() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static PaymentMethod Create(
        Guid organizationId,
        string name,
        int displayOrder,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmedName = name.Trim();
        if (trimmedName.Length > 50)
            throw new ArgumentException("El nombre del medio de pago no puede superar 50 caracteres.", nameof(name));

        if (displayOrder < 1 || displayOrder > 999)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "El orden de visualización debe estar entre 1 y 999.");

        return new PaymentMethod
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = trimmedName,
            NormalizedName = Normalize(trimmedName),
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(string name, int displayOrder, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmedName = name.Trim();
        if (trimmedName.Length > 50)
            throw new ArgumentException("El nombre del medio de pago no puede superar 50 caracteres.", nameof(name));

        if (displayOrder < 1 || displayOrder > 999)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "El orden de visualización debe estar entre 1 y 999.");

        Name = trimmedName;
        NormalizedName = Normalize(trimmedName);
        DisplayOrder = displayOrder;
        UpdatedAtUtc = nowUtc;
    }

    public void SetStatus(bool isActive, DateTime nowUtc)
    {
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
    }

    private static string Normalize(string value)
    {
        var normalizedString = value.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();

        foreach (var c in normalizedString)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).Trim().ToUpperInvariant();
    }
}
