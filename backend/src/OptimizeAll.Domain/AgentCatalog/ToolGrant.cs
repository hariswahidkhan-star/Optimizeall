using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.AgentCatalog;

/// <summary>
/// One capability granted to one agent definition, with optional per-grant constraints such as a
/// domain allow-list or a spend ceiling. The absence of a grant row is denial; there is no
/// "allow all" representation.
/// </summary>
public sealed class ToolGrant : Entity<Guid>
{
    private ToolGrant(
        Guid id,
        AgentDefinitionId agentDefinitionId,
        string toolKey,
        ActionRiskClass riskClass,
        string constraintsJson)
        : base(id)
    {
        AgentDefinitionId = agentDefinitionId;
        ToolKey = toolKey;
        RiskClass = riskClass;
        ConstraintsJson = constraintsJson;
    }

    private ToolGrant()
    {
    }

    public AgentDefinitionId AgentDefinitionId { get; private set; }

    public string ToolKey { get; private set; } = null!;

    /// <summary>
    /// Copied from the registry at grant time so that a later change to a tool's risk classification
    /// cannot retroactively alter what a published definition was reviewed and approved as.
    /// </summary>
    public ActionRiskClass RiskClass { get; private set; }

    /// <summary>JSON object of grant-scoped restrictions. <c>{}</c> means the tool's own defaults apply.</summary>
    public string ConstraintsJson { get; private set; } = "{}";

    internal static ToolGrant Create(
        AgentDefinitionId agentDefinitionId,
        string toolKey,
        ActionRiskClass riskClass,
        string constraintsJson)
    {
        Ensure.NotNullOrWhiteSpace(toolKey);

        return new ToolGrant(
            Guid.CreateVersion7(),
            agentDefinitionId,
            toolKey.Trim(),
            riskClass,
            string.IsNullOrWhiteSpace(constraintsJson) ? "{}" : constraintsJson);
    }
}
