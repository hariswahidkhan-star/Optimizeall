using System.Reflection;
using OptimizeAll.Domain.Audit;
using OptimizeAll.Domain.Common;
using Xunit;

namespace OptimizeAll.Domain.Tests.Audit;

public sealed class AuditEventTests
{
    private static AuditEvent Append(long sequence, string previousHash, string action = AuditActions.ApprovalDecided)
        => AuditEvent.Append(
            TestData.Tenant,
            sequence,
            TestData.Now.AddSeconds(sequence),
            TestData.Alice,
            action,
            resourceType: "ApprovalRequest",
            resourceId: Guid.Parse("0195c0de-0000-7000-8000-0000000000aa"),
            AuditOutcome.Success,
            correlationId: Guid.Parse("0195c0de-0000-7000-8000-0000000000bb"),
            previousHash,
            workspaceId: TestData.Workspace,
            environment: EnvironmentTier.Production);

    [Fact]
    public void The_first_entry_for_a_tenant_links_to_the_genesis_hash()
    {
        AuditEvent first = Append(1, AuditEvent.GenesisHash);

        Assert.True(first.VerifySelf());
        Assert.True(first.VerifyFollows(null));
    }

    [Fact]
    public void An_intact_chain_verifies_end_to_end()
    {
        AuditEvent first = Append(1, AuditEvent.GenesisHash);
        AuditEvent second = Append(2, first.EntryHash);
        AuditEvent third = Append(3, second.EntryHash);

        Assert.True(second.VerifyFollows(first));
        Assert.True(third.VerifyFollows(second));
    }

    [Fact]
    public void Two_entries_with_identical_content_produce_different_hashes_because_the_sequence_differs()
    {
        AuditEvent first = Append(1, AuditEvent.GenesisHash);
        AuditEvent second = Append(2, first.EntryHash);

        Assert.NotEqual(first.EntryHash, second.EntryHash);
    }

    [Fact]
    public void Tampering_with_a_stored_field_is_detected_by_self_verification()
    {
        AuditEvent entry = Append(1, AuditEvent.GenesisHash);

        // Simulate an attacker editing the row directly in the database. The private setter is
        // reached by reflection precisely because no application code path can do this.
        typeof(AuditEvent)
            .GetProperty(nameof(AuditEvent.Action), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entry, "approval.rejected");

        Assert.False(entry.VerifySelf());
    }

    [Fact]
    public void Removing_an_entry_from_the_middle_breaks_the_chain()
    {
        AuditEvent first = Append(1, AuditEvent.GenesisHash);
        AuditEvent second = Append(2, first.EntryHash);
        AuditEvent third = Append(3, second.EntryHash);

        // The auditor now sees only entries 1 and 3.
        Assert.False(third.VerifyFollows(first));
    }

    [Fact]
    public void An_entry_from_another_tenant_cannot_be_spliced_into_this_chain()
    {
        AuditEvent first = Append(1, AuditEvent.GenesisHash);

        AuditEvent foreign = AuditEvent.Append(
            TenantId.New(),
            2,
            TestData.Now,
            TestData.Alice,
            AuditActions.ApprovalDecided,
            "ApprovalRequest",
            null,
            AuditOutcome.Success,
            Guid.NewGuid(),
            first.EntryHash);

        Assert.False(foreign.VerifyFollows(first));
    }

    [Fact]
    public void A_sequence_below_one_is_rejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Append(0, AuditEvent.GenesisHash));
}
