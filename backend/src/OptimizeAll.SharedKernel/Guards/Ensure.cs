using System.Runtime.CompilerServices;

namespace OptimizeAll.SharedKernel.Guards;

/// <summary>
/// Precondition checks for constructor and factory arguments. These guard programmer errors —
/// conditions that should be impossible if callers are correct — and therefore throw rather than
/// returning a <c>Result</c>. Expected, user-caused failures use <c>Result</c> instead.
/// </summary>
public static class Ensure
{
    public static string NotNullOrWhiteSpace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    public static string MaxLength(
        string value,
        int maxLength,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);

        if (value.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value exceeds the maximum length of {maxLength} characters (actual: {value.Length}).",
                paramName);
        }

        return value;
    }

    public static Guid NotEmpty(Guid value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier must not be empty.", paramName);
        }

        return value;
    }

    public static T NotNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return value;
    }

    public static int Positive(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, paramName);
        return value;
    }

    public static decimal NotNegative(decimal value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        return value;
    }

    public static IReadOnlyCollection<T> NotEmpty<T>(
        IReadOnlyCollection<T>? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);

        if (value.Count == 0)
        {
            throw new ArgumentException("Collection must not be empty.", paramName);
        }

        return value;
    }
}
