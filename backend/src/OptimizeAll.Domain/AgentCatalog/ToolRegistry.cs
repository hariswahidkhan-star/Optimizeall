using OptimizeAll.Domain.Common;

namespace OptimizeAll.Domain.AgentCatalog;

/// <summary>
/// The closed set of capabilities an agent may be granted, each bound to a risk class.
/// <para>
/// Risk is assigned to a tool once, here, rather than being decided at each call site. That is what
/// makes "publishing is always gated" a property of the system rather than a convention that holds
/// until someone adds a second publish path.
/// </para>
/// </summary>
public static class ToolRegistry
{
    public const string KnowledgeSearch = "knowledge.search";
    public const string KnowledgeWrite = "knowledge.write";
    public const string WebSearch = "web.search";
    public const string WebFetch = "web.fetch";
    public const string AnalyticsQuery = "analytics.query";
    public const string CrmRead = "crm.read";
    public const string CrmWrite = "crm.write";
    public const string ContentDraft = "content.draft";
    public const string ContentPublish = "content.publish";
    public const string EmailSend = "email.send";
    public const string LinkedInMessage = "linkedin.message";
    public const string AdsSpend = "ads.spend";
    public const string InvoiceIssue = "invoice.issue";
    public const string RepoRead = "repo.read";
    public const string RepoPropose = "repo.propose";
    public const string DeployTrigger = "deploy.trigger";
    public const string TicketWrite = "ticket.write";
    public const string ReportGenerate = "report.generate";
    public const string DataExport = "data.export";
    public const string AgentDelegate = "agent.delegate";
    public const string ScheduleManage = "schedule.manage";
    public const string PolicyEvaluate = "policy.evaluate";

    private static readonly Dictionary<string, ActionRiskClass> RiskByTool = new(StringComparer.Ordinal)
    {
        [KnowledgeSearch] = ActionRiskClass.Read,
        [KnowledgeWrite] = ActionRiskClass.Write,
        [WebSearch] = ActionRiskClass.Read,
        [WebFetch] = ActionRiskClass.Read,
        [AnalyticsQuery] = ActionRiskClass.Read,
        [CrmRead] = ActionRiskClass.Read,
        [CrmWrite] = ActionRiskClass.External,
        [ContentDraft] = ActionRiskClass.Write,
        [ContentPublish] = ActionRiskClass.External,
        [EmailSend] = ActionRiskClass.External,
        [LinkedInMessage] = ActionRiskClass.External,
        [AdsSpend] = ActionRiskClass.Financial,
        [InvoiceIssue] = ActionRiskClass.Financial,
        [RepoRead] = ActionRiskClass.Read,
        [RepoPropose] = ActionRiskClass.Write,
        [DeployTrigger] = ActionRiskClass.Irreversible,
        [TicketWrite] = ActionRiskClass.Write,
        [ReportGenerate] = ActionRiskClass.Write,
        [DataExport] = ActionRiskClass.External,
        [AgentDelegate] = ActionRiskClass.Write,
        [ScheduleManage] = ActionRiskClass.Write,
        [PolicyEvaluate] = ActionRiskClass.Read,
    };

    public static IReadOnlySet<string> All { get; } = RiskByTool.Keys.ToHashSet(StringComparer.Ordinal);

    public static bool IsKnown(string toolKey) => RiskByTool.ContainsKey(toolKey);

    /// <summary>
    /// The risk class of a tool. Throws for an unknown tool rather than defaulting: silently
    /// treating an unrecognised capability as low risk is precisely the failure mode that lets an
    /// ungated external action through.
    /// </summary>
    public static ActionRiskClass RiskOf(string toolKey) => RiskByTool.TryGetValue(toolKey, out ActionRiskClass risk)
        ? risk
        : throw new ArgumentException($"'{toolKey}' is not a registered tool.", nameof(toolKey));

    public static bool TryGetRisk(string toolKey, out ActionRiskClass risk) => RiskByTool.TryGetValue(toolKey, out risk);
}
