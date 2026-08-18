using Microsoft.AspNetCore.Http.HttpResults;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Api.Middleware;

/// <summary>
/// Translates a <see cref="Result{T}"/> into an HTTP response.
/// <para>
/// Centralised so that the status code for a given failure is decided once. Mapping errors at each
/// endpoint produces a surface where the same condition returns 400 in one place and 409 in
/// another, which clients then have to special-case.
/// </para>
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<TValue>(this Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : ToProblem(result.Error);
    }

    public static IResult ToHttpResult<TValue>(this Result<TValue> result, Func<TValue, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.IsSuccess ? onSuccess(result.Value) : ToProblem(result.Error);
    }

    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess ? TypedResults.NoContent() : ToProblem(result.Error);
    }

    /// <summary>
    /// Builds an RFC 9457 problem document.
    /// <para>
    /// An <see cref="ErrorType.Unexpected"/> message is deliberately replaced: internal failure
    /// text routinely contains connection strings, table names and stack context, and returning it
    /// hands an attacker a free reconnaissance channel. The real message is logged, correlated by
    /// the trace id the client is given.
    /// </para>
    /// </summary>
    private static IResult ToProblem(Error error)
    {
        (int status, string title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "The request is invalid."),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Authentication is required."),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "This operation is not permitted."),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "The resource was not found."),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "The request conflicts with the current state."),
            ErrorType.Invariant => (StatusCodes.Status422UnprocessableEntity, "The request would violate a business rule."),
            ErrorType.Exhausted => (StatusCodes.Status429TooManyRequests, "A quota or budget is exhausted."),
            ErrorType.Unavailable => (StatusCodes.Status503ServiceUnavailable, "A dependency is unavailable."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        Dictionary<string, object?> extensions = new(StringComparer.Ordinal)
        {
            ["code"] = error.Code,
        };

        if (error.Details.Count > 0)
        {
            extensions["errors"] = error.Details;
        }

        return TypedResults.Problem(
            detail: error.Type == ErrorType.Unexpected
                ? "The request could not be completed. Quote the trace id when reporting this."
                : error.Message,
            statusCode: status,
            title: title,
            extensions: extensions);
    }
}
