using Demeure.Application.Common;

namespace Demeure.Infrastructure.Persistence;

internal sealed class UnavailableOrganizationContext : IOrganizationContext
{
    public Guid? OrganizationId => null;
}
