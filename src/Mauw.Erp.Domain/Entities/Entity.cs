namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Base class for all domain entities.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime UpdatedAt { get; protected set; }

    protected Entity(Guid id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    protected Entity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    protected void UpdateTimestamp()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
