using System.Text.RegularExpressions;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Common;

/// <summary>
/// A validated email address, normalised to lower case for comparison.
/// <para>
/// The pattern deliberately checks structure only. Full RFC 5322 validation by regular expression
/// is a known dead end, and the authoritative test of deliverability is a delivered message.
/// </para>
/// </summary>
public sealed partial class EmailAddress : ValueObject
{
    public const int MaxLength = 320;

    private EmailAddress(string value) => Value = value;

    public string Value { get; }

    public string Domain => Value[(Value.IndexOf('@', StringComparison.Ordinal) + 1)..];

    public static EmailAddress Create(string value)
    {
        Ensure.NotNullOrWhiteSpace(value);

        string normalised = value.Trim().ToLowerInvariant();

        if (normalised.Length > MaxLength || !EmailPattern().IsMatch(normalised))
        {
            throw new ArgumentException($"'{value}' is not a valid email address.", nameof(value));
        }

        return new EmailAddress(normalised);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
