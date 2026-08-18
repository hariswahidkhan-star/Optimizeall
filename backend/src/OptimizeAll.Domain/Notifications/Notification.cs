using OptimizeAll.Domain.Common;
using OptimizeAll.SharedKernel.Guards;
using OptimizeAll.SharedKernel.Primitives;

namespace OptimizeAll.Domain.Notifications;

public enum NotificationCategory
{
    ApprovalPending = 1,
    ApprovalResolved = 2,
    RunFailed = 3,
    BudgetThreshold = 4,
    SecurityAlert = 5,
    ScheduleFailure = 6,
    WorkflowCompleted = 7,
    ComplianceFinding = 8,
    SystemAnnouncement = 9,
}

public enum NotificationSeverity
{
    Informational = 1,
    Warning = 2,
    Critical = 3,
}

public enum NotificationChannel
{
    InApp = 1,
    Email = 2,
    Webhook = 3,
}

/// <summary>
/// A message directed at one recipient, with per-channel delivery tracking.
/// <para>
/// Delivery state is recorded per channel rather than as a single flag: an approval notification
/// that reached the in-app inbox but failed by email is neither "delivered" nor "failed", and
/// collapsing that to one boolean loses exactly the information an administrator needs.
/// </para>
/// </summary>
public sealed class Notification : AggregateRoot<NotificationId>, ITenantOwned
{
    private readonly List<ChannelDelivery> _deliveries = [];

    private Notification(
        NotificationId id,
        TenantId tenantId,
        UserId recipientId,
        NotificationCategory category,
        NotificationSeverity severity,
        string title,
        string body,
        string? linkUrl,
        DateTimeOffset createdAt)
        : base(id)
    {
        TenantIdentifier = tenantId;
        RecipientId = recipientId;
        Category = category;
        Severity = severity;
        Title = title;
        Body = body;
        LinkUrl = linkUrl;
        CreatedAt = createdAt;
    }

    private Notification()
    {
    }

    public TenantId TenantIdentifier { get; private set; }

    Guid ITenantOwned.TenantId => TenantIdentifier.Value;

    public UserId RecipientId { get; private set; }

    public NotificationCategory Category { get; private set; }

    public NotificationSeverity Severity { get; private set; }

    public string Title { get; private set; } = null!;

    public string Body { get; private set; } = null!;

    /// <summary>Deep link to the resource this notification is about.</summary>
    public string? LinkUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public IReadOnlyList<ChannelDelivery> Deliveries => _deliveries.AsReadOnly();

    public static Notification Create(
        TenantId tenantId,
        UserId recipientId,
        NotificationCategory category,
        NotificationSeverity severity,
        string title,
        string body,
        DateTimeOffset now,
        string? linkUrl = null,
        IEnumerable<NotificationChannel>? channels = null)
    {
        Ensure.NotNullOrWhiteSpace(title);
        Ensure.NotNullOrWhiteSpace(body);

        Notification notification = new(
            NotificationId.New(),
            tenantId,
            recipientId,
            category,
            severity,
            title.Trim(),
            body.Trim(),
            linkUrl,
            now);

        foreach (NotificationChannel channel in channels ?? [NotificationChannel.InApp])
        {
            notification._deliveries.Add(ChannelDelivery.Pending(channel));
        }

        return notification;
    }

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;

    public void RecordDelivered(NotificationChannel channel, DateTimeOffset now)
    {
        ChannelDelivery? delivery = _deliveries.FirstOrDefault(d => d.Channel == channel);
        delivery?.MarkDelivered(now);
    }

    public void RecordDeliveryFailure(NotificationChannel channel, string reason, DateTimeOffset now)
    {
        ChannelDelivery? delivery = _deliveries.FirstOrDefault(d => d.Channel == channel);
        delivery?.MarkFailed(reason, now);
    }

    public bool IsUnread => ReadAt is null;

    public bool HasUndeliveredChannel => _deliveries.Any(d => !d.IsDelivered);
}

public sealed class ChannelDelivery : Entity<Guid>
{
    private ChannelDelivery(Guid id, NotificationChannel channel)
        : base(id)
        => Channel = channel;

    private ChannelDelivery()
    {
    }

    public NotificationChannel Channel { get; private set; }

    public bool IsDelivered { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    internal static ChannelDelivery Pending(NotificationChannel channel) => new(Guid.CreateVersion7(), channel);

    internal void MarkDelivered(DateTimeOffset now)
    {
        IsDelivered = true;
        DeliveredAt = now;
        AttemptCount++;
        LastError = null;
    }

    internal void MarkFailed(string reason, DateTimeOffset now)
    {
        IsDelivered = false;
        AttemptCount++;
        LastError = reason;
        DeliveredAt = now;
    }
}
