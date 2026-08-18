using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Behaviours;

/// <summary>
/// Emits a structured log line per request with correlation, tenant and timing.
/// <para>
/// Request payloads are never logged. A command carrying an outreach message or a customer record
/// would otherwise put personal data into the log pipeline, which is outside the erasure path.
/// </para>
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger,
    ITenantContext tenantContext)
    : IPipelineBehaviour<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;
        long startedAt = Stopwatch.GetTimestamp();

        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = tenantContext.CorrelationId,
            ["TenantId"] = tenantContext.TenantId.ToString(),
            ["Environment"] = tenantContext.Environment.ToString(),
            ["RequestName"] = requestName,
        });

        try
        {
            Result<TResponse> result = await next().ConfigureAwait(false);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);

            if (result.IsSuccess)
            {
                logger.LogInformation("{RequestName} succeeded in {ElapsedMs}ms.", requestName, elapsed.TotalMilliseconds);
            }
            else
            {
                logger.LogWarning(
                    "{RequestName} failed in {ElapsedMs}ms with {ErrorCode} ({ErrorType}).",
                    requestName,
                    elapsed.TotalMilliseconds,
                    result.Error.Code,
                    result.Error.Type);
            }

            return result;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "{RequestName} threw after {ElapsedMs}ms.",
                requestName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            throw;
        }
    }
}
