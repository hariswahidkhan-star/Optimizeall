using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Infrastructure.Persistence;

/// <summary>
/// Maps a strongly-typed identifier to the <see cref="Guid"/> column beneath it.
/// <para>
/// One generic converter serves every identifier type, made possible by the static abstract factory
/// on <see cref="IStronglyTypedId{TSelf}"/>. The alternative — a hand-written converter per id type —
/// is twenty near-identical classes that someone eventually forgets to add when introducing the
/// twenty-first, producing a runtime mapping failure instead of a compile error.
/// </para>
/// <para>
/// The conversions route through <see cref="Create"/> rather than calling <c>TId.From</c> inline:
/// an expression tree cannot contain an access to a static abstract interface member, so the call
/// is wrapped in an ordinary static method the expression tree can reference.
/// </para>
/// </summary>
public sealed class StronglyTypedIdConverter<TId>()
    : ValueConverter<TId, Guid>(id => id.Value, value => Create(value))
    where TId : struct, IStronglyTypedId<TId>
{
    private static TId Create(Guid value) => TId.From(value);
}

/// <summary>
/// Comparer for strongly-typed ids used in keys and change tracking. Without it EF Core falls back
/// to reference-style comparison semantics for the struct in some paths.
/// </summary>
public sealed class StronglyTypedIdComparer<TId>()
    : ValueComparer<TId>(
        (left, right) => left.Value == right.Value,
        id => id.Value.GetHashCode(),
        id => Identity(id))
    where TId : struct, IStronglyTypedId<TId>
{
    // A strongly-typed id is an immutable readonly struct, so the snapshot is the value itself.
    private static TId Identity(TId id) => id;
}
