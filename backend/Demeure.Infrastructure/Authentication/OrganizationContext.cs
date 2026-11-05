using Demeure.Application.Common;

namespace Demeure.Infrastructure.Authentication;

internal sealed class OrganizationContext : IOrganizationContext
{
    public Guid? OrganizationId { get; private set; }

    internal void Set(Guid? organizationId) => OrganizationId = organizationId;
}
