using System.Diagnostics.CodeAnalysis;
using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Properties.Entities;

public sealed class PropertyManagerAssignment : OrganizationEntity
{
    private PropertyManagerAssignment() { }

    [SetsRequiredMembers]
    public PropertyManagerAssignment(Guid organizationId, Guid propertyId, Guid userId)
    {
        OrganizationId = DomainGuard.Id(organizationId, nameof(organizationId));
        PropertyId = DomainGuard.Id(propertyId, nameof(propertyId));
        UserId = DomainGuard.Id(userId, nameof(userId));
    }

    public Guid PropertyId { get; private set; }
    public Guid UserId { get; private set; }
}
