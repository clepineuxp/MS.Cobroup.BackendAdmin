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

        return new PaymentMethod
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = trimmedName,
            NormalizedName = trimmedName.ToUpperInvariant(),
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

        Name = trimmedName;
        NormalizedName = trimmedName.ToUpperInvariant();
        DisplayOrder = displayOrder;
        UpdatedAtUtc = nowUtc;
    }

    public void SetStatus(bool isActive, DateTime nowUtc)
    {
        IsActive = isActive;
        UpdatedAtUtc = nowUtc;
    }
}
