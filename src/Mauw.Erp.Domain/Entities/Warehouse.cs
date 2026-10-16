namespace Mauw.Erp.Domain.Entities;

using Mauw.Erp.Domain.Enums;

public class Warehouse : Entity<Guid>
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public WarehouseType Type { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsSellable { get; private set; }
    public Guid ContactAddressId { get; private set; }
    public ContactAddress Address { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public string? DeletedBy { get; private set; }

    protected Warehouse() : base()
    {
    }

    public Warehouse(
        string code,
        string name,
        WarehouseType type,
        Guid contactAddressId,
        bool isSellable,
        bool isDefault = false,
        string? description = null) : base()
    {
        Code = NormalizeCode(code);
        Name = name.Trim();
        Type = type;
        ContactAddressId = contactAddressId;
        IsSellable = isSellable;
        IsDefault = isDefault;
        IsActive = true;
        Description = NormalizeDescription(description);
    }

    public void Activate()
    {
        IsActive = true;
        UpdateTimestamp();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdateTimestamp();
    }

    public void SetAsDefault()
    {
        IsDefault = true;
        UpdateTimestamp();
    }

    public void UnsetDefault()
    {
        IsDefault = false;
        UpdateTimestamp();
    }

    public void Update(
        string name,
        WarehouseType type,
        Guid contactAddressId,
        bool isSellable,
        string? description)
    {
        Name = name.Trim();
        Type = type;
        ContactAddressId = contactAddressId;
        IsSellable = isSellable;
        Description = NormalizeDescription(description);
        UpdateTimestamp();
    }

    public void SoftDelete(string? deletedBy)
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        DeletedBy = string.IsNullOrWhiteSpace(deletedBy) ? null : deletedBy.Trim();
        UpdateTimestamp();
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
