namespace OptimizeAll.Domain.Common;

/// <summary>
/// The isolation tier a piece of work executes in. This is not a label — it determines which
/// credentials resolve, which integrations are reachable, and how strict the approval policy is.
/// A Development-scoped agent cannot obtain a Production credential, because the secret reference
/// is keyed by the tier and simply does not resolve.
/// </summary>
public enum EnvironmentTier
{
    Development = 1,
    Staging = 2,
    Production = 3,
}

public static class EnvironmentTierExtensions
{
    /// <summary>
    /// True when actions in this tier can affect real customers, real money, or real reputation.
    /// Used to decide whether an approval policy may be relaxed.
    /// </summary>
    public static bool IsProductionLike(this EnvironmentTier tier) => tier == EnvironmentTier.Production;
}
