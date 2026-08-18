using Microsoft.AspNetCore.Diagnostics;

namespace OptimizeAll.Api.Middleware;

/// <summary>
/// Last-resort handler for exceptions that escape the pipeline.
/// <para>
/// Returns a problem document carrying only the trace id. An unhandled exception is by definition
/// a condition nobody anticipated, so its message is the least safe thing to echo to a caller — the
/// detail goes to the log, and the trace id connects the two.
/// </para>
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        logger.LogError(
            exception,
            "Unhandled exception on {Method} {Path} (trace {TraceId}).",
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc9457",
                title = "An unexpected error occurred.",
                status = StatusCodes.Status500InternalServerError,
                detail = "The request could not be completed. Quote the trace id when reporting this.",
                traceId = httpContext.TraceIdentifier,
            },
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}
