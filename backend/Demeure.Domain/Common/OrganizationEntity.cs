namespace Demeure.Domain.Common;

public abstract class OrganizationEntity : Entity
{
    private Guid organizationId;

    public required Guid OrganizationId
    {
        get => organizationId;
        init
        {
            if (organizationId != Guid.Empty && organizationId != value)
                throw new InvalidOperationException("Organization ownership cannot be changed.");
            organizationId = DomainGuard.Id(value, nameof(OrganizationId));
        }
    }
}
