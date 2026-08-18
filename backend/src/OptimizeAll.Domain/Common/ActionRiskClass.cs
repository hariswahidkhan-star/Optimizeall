namespace OptimizeAll.Domain.Common;

/// <summary>
/// How much damage an action can do. This single value drives approval gating, so it is assigned
/// to a tool once, centrally, rather than being decided per call site.
/// </summary>
public enum ActionRiskClass
{
    /// <summary>Retrieves data. Changes nothing anywhere.</summary>
    Read = 1,

    /// <summary>Changes state inside OptimizeAll only. Audited, not gated.</summary>
    Write = 2,

    /// <summary>Causes an effect in a third-party system. Always gated.</summary>
    External = 3,

    /// <summary>Commits money or contractual obligation. Always gated, threshold-tiered.</summary>
    Financial = 4,

    /// <summary>Cannot be undone by the platform. Always gated, two approvers.</summary>
    Irreversible = 5,
}

public static class ActionRiskClassExtensions
{
    /// <summary>
    /// Whether human approval is mandatory for this class. There is deliberately no parameter here:
    /// no tenant setting, feature flag, or environment can turn this off. Tenants may only make
    /// gating stricter, never looser.
    /// </summary>
    public static bool RequiresHumanApproval(this ActionRiskClass riskClass) => riskClass
        is ActionRiskClass.External
        or ActionRiskClass.Financial
        or ActionRiskClass.Irreversible;

    /// <summary>The minimum number of distinct human approvers for this class.</summary>
    public static int MinimumApproverCount(this ActionRiskClass riskClass) => riskClass switch
    {
        ActionRiskClass.Irreversible => 2,
        ActionRiskClass.Financial => 1,
        ActionRiskClass.External => 1,
        _ => 0,
    };

    public static bool IsAtLeast(this ActionRiskClass riskClass, ActionRiskClass floor) => riskClass >= floor;
}
