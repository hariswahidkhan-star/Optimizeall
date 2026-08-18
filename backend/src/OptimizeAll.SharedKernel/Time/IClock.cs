namespace OptimizeAll.SharedKernel.Time;

/// <summary>
/// The single source of "now". Nothing in the platform reads <c>DateTimeOffset.UtcNow</c> directly:
/// scheduling, lease expiry, approval expiry, and budget windows are all time-dependent, and code
/// that cannot have its clock controlled cannot have those behaviours tested.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>The production clock.</summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
