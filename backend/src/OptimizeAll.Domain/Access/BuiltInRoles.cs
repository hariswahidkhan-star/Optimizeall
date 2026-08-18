namespace OptimizeAll.Domain.Access;

/// <summary>
/// Roles the platform ships and maintains. They are immutable at runtime: a tenant that could edit
/// <c>tenant.auditor</c> could quietly grant it write access, which would defeat the purpose of
/// having an independent auditor role at all.
/// </summary>
public static class BuiltInRoles
{
    public const string PlatformOperator = "platform.operator";
    public const string TenantOwner = "tenant.owner";
    public const string Administrator = "tenant.administrator";
    public const string Approver = "tenant.approver";
    public const string Operator = "tenant.operator";
    public const string Analyst = "tenant.analyst";
    public const string Auditor = "tenant.auditor";

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Definitions { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [PlatformOperator] =
            [
                Permissions.Tenant.Provision, Permissions.Tenant.Read, Permissions.Tenant.Manage,
                Permissions.Platform.Escalate, Permissions.Audit.Read, Permissions.Audit.Verify,
            ],

            [TenantOwner] =
            [
                Permissions.Tenant.Read, Permissions.Tenant.Manage,
                Permissions.Workspace.Read, Permissions.Workspace.Manage, Permissions.Workspace.EngageKillSwitch,
                Permissions.Role.Read, Permissions.Role.Manage, Permissions.Role.Assign,
                Permissions.User.Read, Permissions.User.Manage, Permissions.User.Invite,
                Permissions.Agent.Read, Permissions.Agent.Configure, Permissions.Agent.Publish, Permissions.Agent.Invoke,
                Permissions.Run.Read, Permissions.Run.Cancel, Permissions.Run.Replay,
                Permissions.Workflow.Read, Permissions.Workflow.Create, Permissions.Workflow.Cancel,
                Permissions.Approval.Read, Permissions.Approval.Decide,
                Permissions.Approval.DecideFinancial, Permissions.Approval.DecideIrreversible,
                Permissions.Policy.Read, Permissions.Policy.Manage,
                Permissions.Knowledge.Read, Permissions.Knowledge.Write, Permissions.Knowledge.Deprecate,
                Permissions.Schedule.Read, Permissions.Schedule.Manage,
                Permissions.Audit.Read,
                Permissions.Analytics.Read, Permissions.Analytics.Export,
                Permissions.Report.Read, Permissions.Report.Create, Permissions.Report.Distribute,
                Permissions.Finance.Read, Permissions.Finance.Manage,
            ],

            [Administrator] =
            [
                Permissions.Workspace.Read, Permissions.Workspace.Manage, Permissions.Workspace.EngageKillSwitch,
                Permissions.Role.Read, Permissions.Role.Manage, Permissions.Role.Assign,
                Permissions.User.Read, Permissions.User.Manage, Permissions.User.Invite,
                Permissions.Agent.Read, Permissions.Agent.Configure, Permissions.Agent.Publish, Permissions.Agent.Invoke,
                Permissions.Run.Read, Permissions.Run.Cancel, Permissions.Run.Replay,
                Permissions.Workflow.Read, Permissions.Workflow.Create, Permissions.Workflow.Cancel,
                Permissions.Approval.Read, Permissions.Approval.Decide,
                Permissions.Policy.Read, Permissions.Policy.Manage,
                Permissions.Knowledge.Read, Permissions.Knowledge.Write,
                Permissions.Schedule.Read, Permissions.Schedule.Manage,
                Permissions.Audit.Read,
                Permissions.Analytics.Read,
                Permissions.Report.Read, Permissions.Report.Create,
            ],

            // Deliberately narrow: an approver needs to see what they are deciding on and nothing more.
            // Financial and Irreversible authority is granted separately and explicitly.
            [Approver] =
            [
                Permissions.Approval.Read, Permissions.Approval.Decide,
                Permissions.Run.Read, Permissions.Workflow.Read,
                Permissions.Workspace.Read, Permissions.Agent.Read,
            ],

            [Operator] =
            [
                Permissions.Workspace.Read,
                Permissions.Agent.Read, Permissions.Agent.Invoke,
                Permissions.Run.Read, Permissions.Run.Cancel,
                Permissions.Workflow.Read, Permissions.Workflow.Create, Permissions.Workflow.Cancel,
                Permissions.Approval.Read,
                Permissions.Knowledge.Read, Permissions.Knowledge.Write,
                Permissions.Schedule.Read,
                Permissions.Analytics.Read,
                Permissions.Report.Read,
            ],

            [Analyst] =
            [
                Permissions.Workspace.Read,
                Permissions.Analytics.Read,
                Permissions.Report.Read,
                Permissions.Run.Read,
            ],

            // Read-only by construction. An auditor with any write permission is not independent.
            [Auditor] =
            [
                Permissions.Audit.Read, Permissions.Audit.Verify,
                Permissions.Approval.Read,
                Permissions.Run.Read,
                Permissions.Workflow.Read,
                Permissions.Workspace.Read,
                Permissions.Agent.Read,
                Permissions.Policy.Read,
            ],
        };
}
