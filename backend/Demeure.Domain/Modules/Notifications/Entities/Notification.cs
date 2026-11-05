using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Notifications.Entities;

public sealed class Notification : OrganizationEntity
{
    public required Guid RecipientUserId { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public NotificationChannel Channel { get; init; } = NotificationChannel.InApp;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public enum NotificationChannel { InApp, Email }
public enum NotificationStatus { Pending, Sent, Failed }
