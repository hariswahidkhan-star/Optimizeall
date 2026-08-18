using Microsoft.Extensions.Caching.Memory;
using OptimizeAll.Application.Abstractions.Security;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Time;

namespace OptimizeAll.Infrastructure.Security;

/// <summary>
/// Tracks recent re-authentication so a privileged action can require it.
/// <para>
/// The record is written when the identity provider asserts a fresh authentication (an <c>auth_time</c>
/// claim within the required window), and read when a Financial or Irreversible decision, a role
/// change, or an emergency-stop release is attempted.
/// </para>
/// </summary>
public sealed class StepUpVerifier(IMemoryCache cache, IClock clock) : IStepUpVerifier
{
    public Task<bool> IsVerifiedAsync(PrincipalRef principal, TimeSpan maxAge, CancellationToken cancellationToken)
    {
        bool found = cache.TryGetValue(BuildKey(principal), out DateTimeOffset verifiedAt);

        return Task.FromResult(found && clock.UtcNow - verifiedAt <= maxAge);
    }

    /// <summary>
    /// Records a fresh authentication. Called from the authentication pipeline when the token's
    /// <c>auth_time</c> is recent, never from a handler — a request must not be able to assert its
    /// own step-up.
    /// </summary>
    public void RecordVerification(PrincipalRef principal, DateTimeOffset verifiedAt)
        => cache.Set(BuildKey(principal), verifiedAt, TimeSpan.FromMinutes(30));

    private static string BuildKey(PrincipalRef principal) => $"stepup:{principal.Type}:{principal.Id}";
}
