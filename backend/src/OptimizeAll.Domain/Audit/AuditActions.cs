namespace OptimizeAll.Domain.Audit;

/// <summary>
/// The vocabulary of audit action names. Centralised so that a query for
/// <c>approval.decided</c> finds every occurrence rather than the subset that happened to spell it
/// the same way.
/// </summary>
public static class AuditActions
{
    public const string TenantProvisioned = "tenant.provisioned";
    public const string TenantSuspended = "tenant.suspended";
    public const string TenantDeletionRequested = "tenant.deletion_requested";

    public const string WorkspaceCreated = "workspace.created";
    public const string KillSwitchEngaged = "workspace.kill_switch_engaged";
    public const string KillSwitchReleased = "workspace.kill_switch_released";

    public const string UserInvited = "user.invited";
    public const string UserSuspended = "user.suspended";
    public const string UserDeprovisioned = "user.deprovisioned";
    public const string RoleGranted = "access.role_granted";
    public const string RoleRevoked = "access.role_revoked";
    public const string RoleCreated = "access.role_created";
    public const string RoleUpdated = "access.role_updated";

    public const string AgentDefinitionCreated = "agent.definition_created";
    public const string AgentDefinitionPublished = "agent.definition_published";
    public const string AgentDefinitionDeprecated = "agent.definition_deprecated";

    public const string RunQueued = "agent.run_queued";
    public const string RunStarted = "agent.run_started";
    public const string RunCompleted = "agent.run_completed";
    public const string RunCancelled = "agent.run_cancelled";

    public const string ToolInvoked = "tool.invoked";
    public const string ToolDenied = "security.tool_invocation_denied";

    public const string ApprovalRequested = "approval.requested";
    public const string ApprovalDecided = "approval.decided";
    public const string ApprovalExpired = "approval.expired";
    public const string ApprovalPayloadMismatch = "security.approval_payload_mismatch";

    public const string WorkflowStarted = "workflow.started";
    public const string WorkflowCompleted = "workflow.completed";
    public const string WorkflowCancelled = "workflow.cancelled";

    public const string KnowledgeIngested = "knowledge.ingested";
    public const string KnowledgeDeprecated = "knowledge.deprecated";

    public const string DataExported = "data.exported";
    public const string ReportDistributed = "report.distributed";

    public const string PlatformEscalation = "platform.escalation";
    public const string PolicyChanged = "policy.changed";
    public const string AuthenticationFailed = "security.authentication_failed";
    public const string AuthorizationDenied = "security.authorization_denied";
}
