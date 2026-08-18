namespace OptimizeAll.SharedKernel.Primitives;

/// <summary>
/// An immutable object with no identity, compared by the values of its components.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>The components that together define this value's identity.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null)
        {
            return false;
        }

        return GetType() == other.GetType()
            && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => obj is ValueObject value && Equals(value);

    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(GetType());

        foreach (object? component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
