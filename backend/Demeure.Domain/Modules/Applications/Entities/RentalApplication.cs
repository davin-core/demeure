using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Applications.Entities;

public sealed class RentalApplication : OrganizationEntity
{
    public required Guid UnitId { get; init; }
    public required Guid TenantId { get; init; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public RentalApplicationStatus Status { get; set; } = RentalApplicationStatus.Draft;
}

public enum RentalApplicationStatus { Draft, Submitted, UnderReview, Approved, Rejected, Withdrawn }
