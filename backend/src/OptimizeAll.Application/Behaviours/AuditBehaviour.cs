using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Domain.Audit;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Behaviours;

/// <summary>
/// Records the outcome of every auditable request.
/// <para>
/// Registered inside the transaction behaviour so the audit entry commits atomically with the
/// change it describes. An audit written in a separate transaction would eventually disagree with
/// reality — recording a change that rolled back, or missing one that did not.
/// </para>
/// <para>
/// Failures are recorded too. A denied or failed attempt is often the more interesting record.
/// </para>
/// </summary>
public sealed class AuditBehaviour<TRequest, TResponse>(IAuditTrail auditTrail)
    : IPipelineBehaviour<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IAuditableRequest auditable)
        {
            return await next().ConfigureAwait(false);
        }

        Result<TResponse> result;

        try
        {
            result = await next().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await auditTrail.AppendAsync(
                auditable.AuditAction,
                auditable.AuditResourceType,
                auditable.AuditResourceId,
                AuditOutcome.Failure,
                metadataJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    exceptionType = exception.GetType().Name,
                }),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            throw;
        }

        AuditOutcome outcome = result.IsSuccess
            ? AuditOutcome.Success
            : result.Error.Type is ErrorType.Forbidden or ErrorType.Unauthorized
                ? AuditOutcome.Denied
                : AuditOutcome.Failure;

        await auditTrail.AppendAsync(
            auditable.AuditAction,
            auditable.AuditResourceType,
            auditable.AuditResourceId,
            outcome,
            metadataJson: result.IsSuccess
                ? "{}"
                : System.Text.Json.JsonSerializer.Serialize(new
                {
                    errorCode = result.Error.Code,
                    errorType = result.Error.Type.ToString(),
                }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result;
    }
}
