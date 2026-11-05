using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Maintenance.Entities;

public sealed class MaintenanceRequest : OrganizationEntity
{
    public required Guid PropertyId { get; init; }
    public Guid? UnitId { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? ContractorId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Normal;
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;
    public DateTimeOffset? CompletedAt { get; set; }
}

public enum MaintenancePriority { Low, Normal, High, Emergency }
public enum MaintenanceStatus { Open, Assigned, InProgress, Completed, Cancelled }
