using FluentValidation;
using FluentValidation.Results;
using OptimizeAll.Application.Abstractions.Messaging;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Application.Behaviours;

/// <summary>
/// Rejects malformed requests before any handler, transaction, or provider call happens.
/// Runs first so that an invalid request costs nothing beyond the validation itself.
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehaviour<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IValidator<TRequest>[] _validators = [.. validators];

    public async Task<Result<TResponse>> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Length == 0)
        {
            return await next().ConfigureAwait(false);
        }

        ValidationContext<TRequest> context = new(request);

        ValidationResult[] results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))).ConfigureAwait(false);

        ValidationFailure[] failures = [.. results.SelectMany(r => r.Errors).Where(f => f is not null)];

        if (failures.Length == 0)
        {
            return await next().ConfigureAwait(false);
        }

        Dictionary<string, string[]> details = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return Result.Failure<TResponse>(Error.Validation(
            "request.validation_failed",
            $"'{typeof(TRequest).Name}' failed validation.",
            details));
    }
}
