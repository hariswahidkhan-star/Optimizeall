namespace OptimizeAll.Domain.Access;

/// <summary>
/// The closed set of permissions the platform recognises. Declaring them as constants rather than
/// free strings means a typo is a compile error instead of a silent authorisation gap that fails
/// open at the call site.
/// </summary>
public static class Permissions
{
    public static class Tenant
    {
        public const string Read = "tenant:read";
        public const string Manage = "tenant:manage";
        public const string Provision = "tenant:provision";
        public const string Delete = "tenant:delete";
    }

    public static class Workspace
    {
        public const string Read = "workspace:read";
        public const string Manage = "workspace:manage";
        public const string EngageKillSwitch = "workspace:killswitch";
    }

    public static class Role
    {
        public const string Read = "role:read";
        public const string Manage = "role:manage";
        public const string Assign = "role:assign";
    }

    public static class User
    {
        public const string Read = "user:read";
        public const string Manage = "user:manage";
        public const string Invite = "user:invite";
    }

    public static class Agent
    {
        public const string Read = "agent:read";
        public const string Configure = "agent:configure";
        public const string Publish = "agent:publish";
        public const string Invoke = "agent:invoke";
    }

    public static class Run
    {
        public const string Read = "run:read";
        public const string Cancel = "run:cancel";
        public const string Replay = "run:replay";
    }

    public static class Workflow
    {
        public const string Read = "workflow:read";
        public const string Create = "workflow:create";
        public const string Cancel = "workflow:cancel";
    }

    public static class Approval
    {
        public const string Read = "approval:read";
        public const string Decide = "approval:decide";

        /// <summary>Required in addition to <see cref="Decide"/> for Financial-class requests.</summary>
        public const string DecideFinancial = "approval:decide_financial";

        /// <summary>Required in addition to <see cref="Decide"/> for Irreversible-class requests.</summary>
        public const string DecideIrreversible = "approval:decide_irreversible";
    }

    public static class Policy
    {
        public const string Read = "policy:read";
        public const string Manage = "policy:manage";
    }

    public static class Knowledge
    {
        public const string Read = "knowledge:read";
        public const string Write = "knowledge:write";
        public const string Deprecate = "knowledge:deprecate";
    }

    public static class Schedule
    {
        public const string Read = "schedule:read";
        public const string Manage = "schedule:manage";
    }

    public static class Audit
    {
        public const string Read = "audit:read";
        public const string Verify = "audit:verify";
    }

    public static class Analytics
    {
        public const string Read = "analytics:read";
        public const string Export = "analytics:export";
    }

    public static class Report
    {
        public const string Read = "report:read";
        public const string Create = "report:create";
        public const string Distribute = "report:distribute";
    }

    public static class Finance
    {
        public const string Read = "finance:read";
        public const string Manage = "finance:manage";
    }

    public static class Platform
    {
        /// <summary>Cross-tenant access. Audited on every use, time-boxed, never implicit.</summary>
        public const string Escalate = "platform:escalate";
    }

    /// <summary>Every permission the platform knows about. Used to validate custom role definitions.</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Tenant.Read, Tenant.Manage, Tenant.Provision, Tenant.Delete,
        Workspace.Read, Workspace.Manage, Workspace.EngageKillSwitch,
        Role.Read, Role.Manage, Role.Assign,
        User.Read, User.Manage, User.Invite,
        Agent.Read, Agent.Configure, Agent.Publish, Agent.Invoke,
        Run.Read, Run.Cancel, Run.Replay,
        Workflow.Read, Workflow.Create, Workflow.Cancel,
        Approval.Read, Approval.Decide, Approval.DecideFinancial, Approval.DecideIrreversible,
        Policy.Read, Policy.Manage,
        Knowledge.Read, Knowledge.Write, Knowledge.Deprecate,
        Schedule.Read, Schedule.Manage,
        Audit.Read, Audit.Verify,
        Analytics.Read, Analytics.Export,
        Report.Read, Report.Create, Report.Distribute,
        Finance.Read, Finance.Manage,
        Platform.Escalate,
    };
}
