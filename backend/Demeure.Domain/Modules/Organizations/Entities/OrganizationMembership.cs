using System.Diagnostics.CodeAnalysis;
using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Organizations.Entities;

public sealed class OrganizationMembership : OrganizationEntity
{
    private OrganizationMembership() { }

    [SetsRequiredMembers]
    public OrganizationMembership(Guid organizationId, Guid userId, MembershipRole role = MembershipRole.Member)
    {
        OrganizationId = DomainGuard.Id(organizationId, nameof(organizationId));
        UserId = DomainGuard.Id(userId, nameof(userId));
        Role = DomainGuard.Defined(role, nameof(role));
    }

    public Guid UserId { get; private set; }
    public MembershipRole Role { get; private set; } = MembershipRole.Member;
    public MembershipStatus Status { get; private set; } = MembershipStatus.Active;

    public void ChangeRole(MembershipRole role)
    {
        if (Status != MembershipStatus.Active) throw new InvalidOperationException("Only active memberships can change role.");
        Role = DomainGuard.Defined(role, nameof(role));
    }

    public void ChangeStatus(MembershipStatus status)
    {
        DomainGuard.Transition(Status, status, (Status, status) is
            (MembershipStatus.Active, MembershipStatus.Suspended or MembershipStatus.Removed) or
            (MembershipStatus.Suspended, MembershipStatus.Active or MembershipStatus.Removed));
        Status = status;
    }
}

public enum MembershipRole { Owner, Administrator, Manager, Member, Tenant, Contractor }
public enum MembershipStatus { Active, Suspended, Removed }
