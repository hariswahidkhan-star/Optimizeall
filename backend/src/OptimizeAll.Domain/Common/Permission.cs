using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Common;

/// <summary>
/// A single capability, expressed as <c>resource:action</c> — for example <c>approval:decide</c>.
/// Permissions are the atoms of authorisation; roles are merely named bundles of them, which keeps
/// "what can this principal actually do" answerable without interpreting a role hierarchy.
/// </summary>
public sealed class Permission : ValueObject
{
    public const string Wildcard = "*";

    private Permission(string resource, string action)
    {
        Resource = resource;
        Action = action;
    }

    public string Resource { get; }

    public string Action { get; }

    public string Value => $"{Resource}:{Action}";

    public static Permission Parse(string value)
    {
        Ensure.NotNullOrWhiteSpace(value);

        string[] parts = value.Trim().ToLowerInvariant().Split(':', StringSplitOptions.TrimEntries);

        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                $"A permission must be in 'resource:action' form; received '{value}'.",
                nameof(value));
        }

        return new Permission(parts[0], parts[1]);
    }

    public static bool TryParse(string? value, out Permission? permission)
    {
        permission = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            permission = Parse(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether holding this permission satisfies a demand for <paramref name="required"/>.
    /// A wildcard action grants every action on that resource; a wildcard resource grants
    /// everything. Wildcards are intentionally the only form of implication — there is no
    /// inheritance graph to reason about when auditing an access decision.
    /// </summary>
    public bool Satisfies(Permission required)
    {
        ArgumentNullException.ThrowIfNull(required);

        bool resourceMatches = Resource == Wildcard
            || string.Equals(Resource, required.Resource, StringComparison.Ordinal);

        bool actionMatches = Action == Wildcard
            || string.Equals(Action, required.Action, StringComparison.Ordinal);

        return resourceMatches && actionMatches;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Resource;
        yield return Action;
    }
}
