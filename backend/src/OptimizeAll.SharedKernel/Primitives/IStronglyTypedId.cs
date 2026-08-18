namespace OptimizeAll.SharedKernel.Primitives;

/// <summary>
/// A wrapper around a <see cref="Guid"/> that makes identifiers type-distinct.
/// <para>
/// Passing a workspace identifier where a tenant identifier is expected is a security bug, not a
/// typo — and with bare <see cref="Guid"/> parameters the compiler cannot see the difference.
/// The static abstract factory lets the persistence layer register one generic value converter
/// for every identifier type rather than one converter per type.
/// </para>
/// </summary>
public interface IStronglyTypedId<out TSelf>
    where TSelf : struct, IStronglyTypedId<TSelf>
{
    Guid Value { get; }

    static abstract TSelf From(Guid value);

    static abstract TSelf New();
}
