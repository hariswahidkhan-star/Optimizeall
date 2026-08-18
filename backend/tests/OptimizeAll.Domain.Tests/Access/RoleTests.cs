using OptimizeAll.Domain.Access;
using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Results;
using Xunit;

namespace OptimizeAll.Domain.Tests.Access;

public sealed class RoleTests
{
    private static readonly HashSet<string> AdministratorGrants =
        new(BuiltInRoles.Definitions[BuiltInRoles.Administrator]);

    [Fact]
    public void A_custom_role_cannot_grant_a_permission_the_granter_lacks()
    {
        // An administrator does not hold financial approval authority, so they must not be able to
        // mint a role that does and then assign it to themselves.
        Result<Role> created = Role.CreateCustom(
            TestData.Tenant,
            "finance-approver",
            "Finance Approver",
            "Can approve spend.",
            [Permissions.Approval.DecideFinancial],
            AdministratorGrants);

        Assert.True(created.IsFailure);
        Assert.Equal("role.privilege_escalation", created.Error.Code);
    }

    [Fact]
    public void A_custom_role_may_grant_permissions_the_granter_holds()
    {
        Result<Role> created = Role.CreateCustom(
            TestData.Tenant,
            "content-reviewer",
            "Content Reviewer",
            "Reviews drafts.",
            [Permissions.Knowledge.Read, Permissions.Run.Read],
            AdministratorGrants);

        Assert.True(created.IsSuccess);
        Assert.Contains(Permissions.Knowledge.Read, created.Value.Permissions);
    }

    [Fact]
    public void An_unrecognised_permission_is_rejected()
    {
        Result<Role> created = Role.CreateCustom(
            TestData.Tenant, "bogus", "Bogus", "", ["everything:always"], new HashSet<string>(Permissions.All));

        Assert.True(created.IsFailure);
        Assert.Equal("role.unknown_permission", created.Error.Code);
    }

    [Fact]
    public void A_built_in_role_key_cannot_be_redefined()
    {
        Result<Role> created = Role.CreateCustom(
            TestData.Tenant, BuiltInRoles.Auditor, "My Auditor", "", [], new HashSet<string>(Permissions.All));

        Assert.True(created.IsFailure);
        Assert.Equal("role.reserved_key", created.Error.Code);
    }

    [Fact]
    public void A_built_in_role_cannot_be_modified()
    {
        Role auditor = Role.CreateBuiltIn(
            TestData.Tenant, BuiltInRoles.Auditor, BuiltInRoles.Definitions[BuiltInRoles.Auditor]);

        Result updated = auditor.UpdatePermissions([Permissions.Tenant.Delete], new HashSet<string>(Permissions.All));

        Assert.True(updated.IsFailure);
        Assert.Equal("role.builtin_immutable", updated.Error.Code);
    }

    [Fact]
    public void The_auditor_role_holds_no_write_permission()
    {
        IReadOnlyList<string> auditorGrants = BuiltInRoles.Definitions[BuiltInRoles.Auditor];

        string[] writeLike = [.. auditorGrants.Where(p =>
            p.EndsWith(":manage", StringComparison.Ordinal)
            || p.EndsWith(":write", StringComparison.Ordinal)
            || p.EndsWith(":create", StringComparison.Ordinal)
            || p.EndsWith(":delete", StringComparison.Ordinal)
            || p.EndsWith(":decide", StringComparison.Ordinal))];

        Assert.Empty(writeLike);
    }

    [Fact]
    public void The_approver_role_does_not_carry_financial_or_irreversible_authority_implicitly()
    {
        IReadOnlyList<string> approverGrants = BuiltInRoles.Definitions[BuiltInRoles.Approver];

        Assert.DoesNotContain(Permissions.Approval.DecideFinancial, approverGrants);
        Assert.DoesNotContain(Permissions.Approval.DecideIrreversible, approverGrants);
    }

    [Fact]
    public void Every_built_in_role_grants_only_recognised_permissions()
    {
        foreach ((string roleKey, IReadOnlyList<string> grants) in BuiltInRoles.Definitions)
        {
            foreach (string permission in grants)
            {
                Assert.True(
                    Permissions.All.Contains(permission),
                    $"Role '{roleKey}' grants unrecognised permission '{permission}'.");
            }
        }
    }
}

public sealed class RoleAssignmentTests
{
    [Fact]
    public void A_grant_scoped_to_production_does_not_apply_in_development()
    {
        RoleAssignment assignment = RoleAssignment.Grant(
            TestData.Tenant,
            TestData.Alice,
            RoleId.New(),
            TestData.Workspace,
            EnvironmentTier.Production,
            TestData.ApproverBob,
            expiresAt: null,
            TestData.Now).Value;

        Assert.True(assignment.AppliesTo(TestData.Workspace, EnvironmentTier.Production, TestData.Now));
        Assert.False(assignment.AppliesTo(TestData.Workspace, EnvironmentTier.Development, TestData.Now));
    }

    [Fact]
    public void A_grant_with_no_environment_applies_everywhere()
    {
        RoleAssignment assignment = RoleAssignment.Grant(
            TestData.Tenant, TestData.Alice, RoleId.New(), TestData.Workspace,
            environment: null, TestData.ApproverBob, null, TestData.Now).Value;

        Assert.True(assignment.AppliesTo(TestData.Workspace, EnvironmentTier.Development, TestData.Now));
        Assert.True(assignment.AppliesTo(TestData.Workspace, EnvironmentTier.Production, TestData.Now));
    }

    [Fact]
    public void A_grant_scoped_to_one_workspace_does_not_apply_to_another()
    {
        RoleAssignment assignment = RoleAssignment.Grant(
            TestData.Tenant, TestData.Alice, RoleId.New(), TestData.Workspace,
            EnvironmentTier.Production, TestData.ApproverBob, null, TestData.Now).Value;

        Assert.False(assignment.AppliesTo(WorkspaceId.New(), EnvironmentTier.Production, TestData.Now));
    }

    [Fact]
    public void An_expired_grant_stops_applying()
    {
        RoleAssignment assignment = RoleAssignment.Grant(
            TestData.Tenant, TestData.Alice, RoleId.New(), TestData.Workspace,
            EnvironmentTier.Production, TestData.ApproverBob,
            expiresAt: TestData.Now.AddHours(1), TestData.Now).Value;

        Assert.True(assignment.AppliesTo(TestData.Workspace, EnvironmentTier.Production, TestData.Now));
        Assert.False(assignment.AppliesTo(TestData.Workspace, EnvironmentTier.Production, TestData.Now.AddHours(2)));
    }

    [Fact]
    public void A_grant_cannot_be_created_already_expired()
    {
        Result<RoleAssignment> granted = RoleAssignment.Grant(
            TestData.Tenant, TestData.Alice, RoleId.New(), TestData.Workspace,
            EnvironmentTier.Production, TestData.ApproverBob,
            expiresAt: TestData.Now.AddHours(-1), TestData.Now);

        Assert.True(granted.IsFailure);
        Assert.Equal("role_assignment.expiry_in_past", granted.Error.Code);
    }
}
