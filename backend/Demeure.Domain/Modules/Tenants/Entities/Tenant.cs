using System.Diagnostics.CodeAnalysis;
using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Tenants.Entities;

public sealed class Tenant : OrganizationEntity
{
    private Tenant() { }

    [SetsRequiredMembers]
    public Tenant(Guid organizationId, string firstName, string lastName,
        Guid? userId = null, string? email = null, string? phone = null)
    {
        OrganizationId = DomainGuard.Id(organizationId, nameof(organizationId));
        UserId = DomainGuard.OptionalId(userId, nameof(userId));
        UpdateContact(firstName, lastName, email, phone);
    }

    public Guid? UserId { get; private set; }
    public string FirstName { get; private set; } = "";
    public string LastName { get; private set; } = "";
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public TenantStatus Status { get; private set; } = TenantStatus.Active;

    public void UpdateContact(string firstName, string lastName, string? email = null, string? phone = null)
    {
        EnsureEditable();
        var first = DomainGuard.Text(firstName, 100, nameof(firstName));
        var last = DomainGuard.Text(lastName, 100, nameof(lastName));
        var validatedEmail = string.IsNullOrWhiteSpace(email) ? null : DomainGuard.Email(email, nameof(email));
        var validatedPhone = DomainGuard.OptionalText(phone, 30, nameof(phone));
        FirstName = first;
        LastName = last;
        Email = validatedEmail;
        Phone = validatedPhone;
    }

    public void LinkUser(Guid userId)
    {
        EnsureEditable();
        if (UserId.HasValue) throw new InvalidOperationException("This tenant already has a linked user.");
        UserId = DomainGuard.Id(userId, nameof(userId));
    }

    public void UnlinkUser()
    {
        EnsureEditable();
        if (!UserId.HasValue) throw new InvalidOperationException("This tenant has no linked user.");
        UserId = null;
    }

    public void ChangeStatus(TenantStatus status)
    {
        DomainGuard.Transition(Status, status, (Status, status) is
            (TenantStatus.Active, TenantStatus.Inactive or TenantStatus.Archived) or
            (TenantStatus.Inactive, TenantStatus.Active or TenantStatus.Archived));
        Status = status;
    }

    private void EnsureEditable()
    {
        if (Status == TenantStatus.Archived) throw new InvalidOperationException("An archived tenant cannot be edited.");
    }
}

public enum TenantStatus { Active, Inactive, Archived }
