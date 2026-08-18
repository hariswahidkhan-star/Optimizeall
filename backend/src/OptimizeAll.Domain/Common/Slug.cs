using System.Text.RegularExpressions;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Common;

/// <summary>
/// A URL-safe, human-readable identifier. Constrained so that a slug can appear in a hostname,
/// a path segment, and a log line without escaping.
/// </summary>
public sealed partial class Slug : ValueObject
{
    public const int MaxLength = 63;

    private Slug(string value) => Value = value;

    public string Value { get; }

    public static Slug Create(string value)
    {
        Ensure.NotNullOrWhiteSpace(value);

        string normalised = value.Trim().ToLowerInvariant();

        if (normalised.Length > MaxLength)
        {
            throw new ArgumentException(
                $"A slug may be at most {MaxLength} characters; received {normalised.Length}.",
                nameof(value));
        }

        if (!SlugPattern().IsMatch(normalised))
        {
            throw new ArgumentException(
                $"A slug must be lower-case alphanumeric with single hyphens between segments; received '{value}'.",
                nameof(value));
        }

        return new Slug(normalised);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
