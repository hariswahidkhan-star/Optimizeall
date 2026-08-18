using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Infrastructure.Security;

/// <summary>
/// Resolves a secret for a specific (tenant, workspace, environment) triple from Azure Key Vault.
/// <para>
/// The scope is part of the secret's name, which is what makes environment isolation structural
/// rather than procedural: a Development-scoped agent asking for a production credential looks up a
/// name that does not exist. There is no code path where the wrong-scope secret is fetched and then
/// rejected, because there is nothing to reject.
/// </para>
/// </summary>
public sealed class KeyVaultSecretResolver(
    SecretClient secretClient,
    IMemoryCache cache,
    ILogger<KeyVaultSecretResolver> logger)
    : ISecretResolver
{
    /// <summary>
    /// Short enough that a rotation takes effect without a redeploy, long enough that Key Vault is
    /// not consulted on every provider call.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<Result<string>> ResolveAsync(
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string secretName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        string scopedName = BuildSecretName(tenantId, workspaceId, environment, secretName);

        if (cache.TryGetValue(scopedName, out string? cached) && cached is not null)
        {
            return Result.Success(cached);
        }

        try
        {
            Response<KeyVaultSecret> response = await secretClient
                .GetSecretAsync(scopedName, cancellationToken: cancellationToken).ConfigureAwait(false);

            string value = response.Value.Value;
            cache.Set(scopedName, value, CacheDuration);

            return Result.Success(value);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            // Logged without the secret name's scope prefix being treated as sensitive — the name
            // is not the secret. The value never reaches a log under any condition.
            logger.LogWarning(
                "Secret '{SecretName}' is not configured for {Environment} in this workspace.",
                secretName,
                environment);

            return Result.Failure<string>(Error.NotFound(
                "secret.not_found",
                $"No '{secretName}' secret is configured for this workspace and environment."));
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(exception, "Key Vault request failed with status {Status}.", exception.Status);

            return Result.Failure<string>(Error.Unavailable(
                "secret.vault_unavailable",
                "The secret store could not be reached."));
        }
    }

    /// <summary>
    /// Key Vault names permit alphanumerics and hyphens only, so the scope is encoded as a
    /// hyphenated path. Including the tenant and workspace ids in full keeps names unambiguous
    /// across a large tenant estate.
    /// </summary>
    private static string BuildSecretName(
        TenantId tenantId,
        WorkspaceId workspaceId,
        EnvironmentTier environment,
        string secretName)
        => $"t-{tenantId.Value:N}-w-{workspaceId.Value:N}-{environment.ToString().ToLowerInvariant()}-{secretName.ToLowerInvariant()}";
}
