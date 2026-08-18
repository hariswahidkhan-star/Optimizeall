using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.AgentCatalog;

public enum AgentDefinitionStatus
{
    Draft = 1,
    Published = 2,
    Deprecated = 3,
}

/// <summary>
/// The versioned, declarative specification of one agent: what it is for, which model runs it,
/// what it may do, what it may remember, and what it costs.
/// <para>
/// A published version is immutable. Editing produces version <c>n+1</c>. This is what makes an
/// agent run reproducible six months later, and what stops a prompt edit from silently changing the
/// meaning of every historical run that referenced "the SEO agent".
/// </para>
/// </summary>
public sealed class AgentDefinition : AggregateRoot<AgentDefinitionId>, IAuditable, ISoftDeletable, ITenantOwned
{
    private readonly List<ToolGrant> _toolGrants = [];
    private readonly List<KpiDefinition> _kpis = [];

    private AgentDefinition(
        AgentDefinitionId id,
        TenantId tenantId,
        WorkspaceId workspaceId,
        string agentKey,
        int version,
        string displayName,
        string mission,
        string systemPrompt,
        ModelPolicy modelPolicy,
        MemoryPolicy memoryPolicy,
        BudgetPolicy budgetPolicy,
        ActionRiskClass maxRiskClass)
        : base(id)
    {
        TenantIdentifier = tenantId;
        WorkspaceId = workspaceId;
        AgentKey = agentKey;
        DefinitionVersion = version;
        DisplayName = displayName;
        Mission = mission;
        SystemPrompt = systemPrompt;
        ModelPolicy = modelPolicy;
        MemoryPolicy = memoryPolicy;
        BudgetPolicy = budgetPolicy;
        MaxRiskClass = maxRiskClass;
        Status = AgentDefinitionStatus.Draft;
    }

    private AgentDefinition()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public WorkspaceId WorkspaceId { get; private set; }

    /// <summary>Stable machine key, e.g. <c>seo-agent</c>. Never reused across different agents.</summary>
    public string AgentKey { get; private set; } = null!;

    /// <summary>
    /// Monotonically increasing per <see cref="AgentKey"/> within a workspace. Distinct from
    /// <see cref="AggregateRoot{TId}.Version"/>, which is the optimistic-concurrency token.
    /// </summary>
    public int DefinitionVersion { get; private set; }

    public string DisplayName { get; private set; } = null!;

    public string Mission { get; private set; } = null!;

    public string SystemPrompt { get; private set; } = null!;

    public ModelPolicy ModelPolicy { get; private set; } = null!;

    public MemoryPolicy MemoryPolicy { get; private set; } = null!;

    public BudgetPolicy BudgetPolicy { get; private set; } = null!;

    /// <summary>
    /// The ceiling on what this agent may even propose. An agent whose maximum is
    /// <see cref="ActionRiskClass.Write"/> cannot be granted a publishing tool, so a
    /// misconfiguration is caught at definition time rather than discovered in production.
    /// </summary>
    public ActionRiskClass MaxRiskClass { get; private set; }

    public ApprovalPolicyId? ApprovalPolicyId { get; private set; }

    public AgentDefinitionStatus Status { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyList<ToolGrant> ToolGrants => _toolGrants.AsReadOnly();

    public IReadOnlyList<KpiDefinition> Kpis => _kpis.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static Result<AgentDefinition> CreateDraft(
        TenantId tenantId,
        WorkspaceId workspaceId,
        string agentKey,
        int version,
        string displayName,
        string mission,
        string systemPrompt,
        ModelPolicy modelPolicy,
        MemoryPolicy memoryPolicy,
        BudgetPolicy budgetPolicy,
        ActionRiskClass maxRiskClass)
    {
        Ensure.NotNullOrWhiteSpace(agentKey);
        Ensure.NotNullOrWhiteSpace(displayName);
        Ensure.NotNullOrWhiteSpace(mission);
        Ensure.NotNullOrWhiteSpace(systemPrompt);
        Ensure.NotNull(modelPolicy);
        Ensure.NotNull(memoryPolicy);
        Ensure.NotNull(budgetPolicy);

        if (version < 1)
        {
            return Result.Failure<AgentDefinition>(Error.Validation(
                "agent.invalid_version",
                "Agent definition versions start at 1."));
        }

        return Result.Success(new AgentDefinition(
            AgentDefinitionId.New(),
            tenantId,
            workspaceId,
            agentKey.Trim().ToLowerInvariant(),
            version,
            displayName.Trim(),
            mission.Trim(),
            systemPrompt,
            modelPolicy,
            memoryPolicy,
            budgetPolicy,
            maxRiskClass));
    }

    /// <summary>
    /// Grants a capability. Refused when the tool's risk exceeds this agent's declared ceiling —
    /// the ceiling is the reviewable statement of intent, and a grant that quietly raises it would
    /// make that statement meaningless.
    /// </summary>
    public Result GrantTool(string toolKey, string constraintsJson = "{}")
    {
        if (Status != AgentDefinitionStatus.Draft)
        {
            return Result.Failure(Error.Conflict(
                "agent.published_immutable",
                "A published agent definition cannot be modified. Create a new version instead."));
        }

        if (!ToolRegistry.TryGetRisk(toolKey, out ActionRiskClass risk))
        {
            return Result.Failure(Error.Validation(
                "agent.unknown_tool",
                $"'{toolKey}' is not a registered tool."));
        }

        if (risk > MaxRiskClass)
        {
            return Result.Failure(Error.Forbidden(
                "agent.risk_ceiling_exceeded",
                $"Tool '{toolKey}' is {risk}, which exceeds this agent's declared ceiling of {MaxRiskClass}."));
        }

        if (_toolGrants.Any(g => string.Equals(g.ToolKey, toolKey, StringComparison.Ordinal)))
        {
            return Result.Success();
        }

        _toolGrants.Add(ToolGrant.Create(Id, toolKey, risk, constraintsJson));
        return Result.Success();
    }

    public Result RevokeTool(string toolKey)
    {
        if (Status != AgentDefinitionStatus.Draft)
        {
            return Result.Failure(Error.Conflict(
                "agent.published_immutable",
                "A published agent definition cannot be modified. Create a new version instead."));
        }

        _toolGrants.RemoveAll(g => string.Equals(g.ToolKey, toolKey, StringComparison.Ordinal));
        return Result.Success();
    }

    public Result AddKpi(KpiDefinition kpi)
    {
        if (Status != AgentDefinitionStatus.Draft)
        {
            return Result.Failure(Error.Conflict(
                "agent.published_immutable",
                "A published agent definition cannot be modified. Create a new version instead."));
        }

        Ensure.NotNull(kpi);
        _kpis.RemoveAll(k => string.Equals(k.Key, kpi.Key, StringComparison.Ordinal));
        _kpis.Add(kpi);
        return Result.Success();
    }

    public Result AttachApprovalPolicy(ApprovalPolicyId policyId)
    {
        if (Status != AgentDefinitionStatus.Draft)
        {
            return Result.Failure(Error.Conflict(
                "agent.published_immutable",
                "A published agent definition cannot be modified. Create a new version instead."));
        }

        ApprovalPolicyId = policyId;
        return Result.Success();
    }

    /// <summary>
    /// Freezes this version. After publication the definition is a historical record that runs
    /// point at, so it must never change again.
    /// </summary>
    public Result Publish(DateTimeOffset now)
    {
        if (Status == AgentDefinitionStatus.Published)
        {
            return Result.Success();
        }

        if (Status == AgentDefinitionStatus.Deprecated)
        {
            return Result.Failure(Error.Conflict(
                "agent.deprecated",
                "A deprecated definition cannot be republished."));
        }

        if (_toolGrants.Count == 0 && MaxRiskClass > ActionRiskClass.Read)
        {
            return Result.Failure(Error.Invariant(
                "agent.no_tools_granted",
                "An agent with a risk ceiling above Read must be granted at least one tool, " +
                "otherwise its ceiling misrepresents what it can do."));
        }

        if (MaxRiskClass.RequiresHumanApproval() && ApprovalPolicyId is null)
        {
            return Result.Failure(Error.Invariant(
                "agent.approval_policy_required",
                $"An agent that can propose {MaxRiskClass} actions must have an approval policy attached."));
        }

        Status = AgentDefinitionStatus.Published;
        PublishedAt = now;
        Raise(new AgentDefinitionPublished(Id, TenantIdentifier, WorkspaceId, AgentKey, DefinitionVersion, now));
        return Result.Success();
    }

    public Result Deprecate(DateTimeOffset now)
    {
        if (Status != AgentDefinitionStatus.Published)
        {
            return Result.Failure(Error.Conflict(
                "agent.not_published",
                "Only a published definition can be deprecated."));
        }

        Status = AgentDefinitionStatus.Deprecated;
        Raise(new AgentDefinitionDeprecated(Id, TenantIdentifier, AgentKey, DefinitionVersion, now));
        return Result.Success();
    }

    /// <summary>
    /// The authorisation check performed before every tool call. Absence of a grant is denial —
    /// there is no default-allow branch anywhere in this method.
    /// </summary>
    public bool IsToolGranted(string toolKey)
        => _toolGrants.Any(g => string.Equals(g.ToolKey, toolKey, StringComparison.Ordinal));

    public ToolGrant? FindGrant(string toolKey)
        => _toolGrants.FirstOrDefault(g => string.Equals(g.ToolKey, toolKey, StringComparison.Ordinal));

    public bool IsExecutable => Status == AgentDefinitionStatus.Published && DeletedAt is null;

    public void StampCreated(DateTimeOffset at, Guid? by)
    {
        CreatedAt = at;
        CreatedBy = by;
    }

    public void StampUpdated(DateTimeOffset at, Guid? by)
    {
        UpdatedAt = at;
        UpdatedBy = by;
    }

    public void MarkDeleted(DateTimeOffset at) => DeletedAt = at;
}
