namespace OptimizeAll.SharedKernel.Results;

/// <summary>
/// A structured, machine-readable failure. Errors are values, not exceptions: the platform
/// distinguishes between "this operation legitimately did not succeed" (an <see cref="Error"/>)
/// and "the process is in an unexpected state" (an exception). Only the latter is thrown.
/// </summary>
public sealed record Error
{
    private Error(string code, string message, ErrorType type, IReadOnlyDictionary<string, string[]>? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
        Type = type;
        Details = details ?? new Dictionary<string, string[]>();
    }

    public string Code { get; }

    public string Message { get; }

    public ErrorType Type { get; }

    /// <summary>Field-level details, keyed by property path. Empty for non-validation errors.</summary>
    public IReadOnlyDictionary<string, string[]> Details { get; }

    /// <summary>The caller supplied something invalid. Maps to HTTP 400.</summary>
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null)
        => new(code, message, ErrorType.Validation, details);

    /// <summary>The requested resource does not exist, or is not visible to this principal. Maps to HTTP 404.</summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>The principal is authenticated but lacks the required permission. Maps to HTTP 403.</summary>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>The principal is not authenticated. Maps to HTTP 401.</summary>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    /// <summary>The operation contradicts the current state of the resource. Maps to HTTP 409.</summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>A domain invariant would be violated. Maps to HTTP 422.</summary>
    public static Error Invariant(string code, string message) => new(code, message, ErrorType.Invariant);

    /// <summary>A quota, budget, or rate limit is exhausted. Maps to HTTP 429.</summary>
    public static Error Exhausted(string code, string message) => new(code, message, ErrorType.Exhausted);

    /// <summary>A dependency failed in a way the caller may retry. Maps to HTTP 502/503.</summary>
    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);

    /// <summary>An unexpected condition. Maps to HTTP 500 and never leaks its message to the client.</summary>
    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);

    public override string ToString() => $"{Code}: {Message}";
}

public enum ErrorType
{
    Validation = 1,
    NotFound = 2,
    Forbidden = 3,
    Unauthorized = 4,
    Conflict = 5,
    Invariant = 6,
    Exhausted = 7,
    Unavailable = 8,
    Unexpected = 9,
}
