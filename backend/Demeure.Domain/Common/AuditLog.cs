namespace Demeure.Domain.Common;

public sealed class AuditLog : OrganizationEntity
{
    public Guid? ActorUserId { get; init; }
    public Guid? PlatformActorUserId { get; init; }
    public string? Reason { get; init; }
    public required string Action { get; init; }
    public required string ResourceType { get; init; }
    public Guid? ResourceId { get; init; }
    public string? TraceId { get; init; }
}
