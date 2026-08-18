namespace OptimizeAll.SharedKernel.Primitives;

/// <summary>
/// An object with a stable identity whose attributes may change over its lifetime.
/// Equality is identity-based: two entities with the same id are the same entity,
/// regardless of their current field values.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : struct
{
    protected Entity(TId id) => Id = id;

    /// <summary>Required by EF Core materialisation. Not for application use.</summary>
    protected Entity()
    {
    }

    public TId Id { get; protected init; }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return GetType() == other.GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => obj is Entity<TId> entity && Equals(entity);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
