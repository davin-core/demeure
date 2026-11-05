using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Contractors.Entities;

public sealed class Contractor : OrganizationEntity
{
    public Guid? UserId { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Trade { get; set; }
    public ContractorStatus Status { get; set; } = ContractorStatus.Active;
}

public enum ContractorStatus { Active, Inactive, Archived }
