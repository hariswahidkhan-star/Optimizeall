using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Domain.Access;

public enum UserStatus
{
    Invited = 1,
    Active = 2,
    Suspended = 3,
    Deprovisioned = 4,
}

/// <summary>
/// A human principal. Credentials are never stored here — authentication is delegated to the
/// identity provider, and this aggregate holds only the platform-side projection of that identity.
/// </summary>
public sealed class User : AggregateRoot<UserId>, IAuditable, ISoftDeletable, ITenantOwned
{
    private User(UserId id, TenantId tenantId, EmailAddress email, string displayName, string? externalSubject)
        : base(id)
    {
        TenantIdentifier = tenantId;
        Email = email;
        DisplayName = displayName;
        ExternalSubject = externalSubject;
        Status = externalSubject is null ? UserStatus.Invited : UserStatus.Active;
    }

    private User()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public EmailAddress Email { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    /// <summary>The OIDC <c>sub</c> claim. Null until the invited user first signs in.</summary>
    public string? ExternalSubject { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static User Invite(TenantId tenantId, EmailAddress email, string displayName, DateTimeOffset now)
    {
        Ensure.NotEmpty(tenantId.Value);
        Ensure.NotNull(email);
        Ensure.MaxLength(Ensure.NotNullOrWhiteSpace(displayName), 200);

        User user = new(UserId.New(), tenantId, email, displayName.Trim(), externalSubject: null);
        user.Raise(new UserInvited(user.Id, tenantId, email.Value, now));
        return user;
    }

    /// <summary>
    /// Binds the platform account to an identity-provider subject on first sign-in. The binding is
    /// permanent: rebinding an existing account to a different subject would let anyone who can
    /// create an IdP account inherit an existing user's role assignments.
    /// </summary>
    public Result LinkExternalIdentity(string externalSubject, DateTimeOffset now)
    {
        Ensure.NotNullOrWhiteSpace(externalSubject);

        if (ExternalSubject is not null && !string.Equals(ExternalSubject, externalSubject, StringComparison.Ordinal))
        {
            return Result.Failure(Error.Conflict(
                "user.identity_already_linked",
                "This account is already linked to a different identity-provider subject."));
        }

        if (Status == UserStatus.Deprovisioned)
        {
            return Result.Failure(Error.Forbidden(
                "user.deprovisioned",
                "A deprovisioned account cannot be re-linked."));
        }

        ExternalSubject = externalSubject;

        if (Status == UserStatus.Invited)
        {
            Status = UserStatus.Active;
            Raise(new UserActivated(Id, TenantIdentifier, now));
        }

        return Result.Success();
    }

    public void RecordLogin(DateTimeOffset at) => LastLoginAt = at;

    public Result Suspend(DateTimeOffset now)
    {
        if (Status == UserStatus.Deprovisioned)
        {
            return Result.Failure(Error.Conflict(
                "user.deprovisioned",
                "A deprovisioned account cannot be suspended."));
        }

        Status = UserStatus.Suspended;
        Raise(new UserSuspended(Id, TenantIdentifier, now));
        return Result.Success();
    }

    public Result Reinstate()
    {
        if (Status != UserStatus.Suspended)
        {
            return Result.Failure(Error.Conflict(
                "user.not_suspended",
                "Only a suspended account can be reinstated."));
        }

        Status = UserStatus.Active;
        return Result.Success();
    }

    /// <summary>
    /// Terminal state. Deprovisioning severs the identity link so that the same identity-provider
    /// subject cannot silently resume the account, and every role assignment must be revoked
    /// separately by the caller.
    /// </summary>
    public void Deprovision(DateTimeOffset now)
    {
        Status = UserStatus.Deprovisioned;
        ExternalSubject = null;
        DeletedAt = now;
        Raise(new UserDeprovisioned(Id, TenantIdentifier, now));
    }

    public void Rename(string displayName)
        => DisplayName = Ensure.MaxLength(Ensure.NotNullOrWhiteSpace(displayName), 200).Trim();

    public bool CanAuthenticate => Status == UserStatus.Active;

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

public sealed record UserInvited(UserId UserId, TenantId TenantId, string Email, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt)
{
    public override string EventType => "user.invited";
}

public sealed record UserActivated(UserId UserId, TenantId TenantId, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt)
{
    public override string EventType => "user.activated";
}

public sealed record UserSuspended(UserId UserId, TenantId TenantId, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt)
{
    public override string EventType => "user.suspended";
}

public sealed record UserDeprovisioned(UserId UserId, TenantId TenantId, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt)
{
    public override string EventType => "user.deprovisioned";
}
