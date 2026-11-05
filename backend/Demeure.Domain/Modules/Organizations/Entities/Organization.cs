using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Organizations.Entities;

public sealed class Organization : Entity
{
    private Organization() { }

    public Organization(string name, string slug, Guid? id = null) : base(id ?? Guid.NewGuid())
    {
        var validatedName = DomainGuard.Text(name, 200, nameof(name));
        var validatedSlug = DomainGuard.Text(slug, 100, nameof(slug)).ToLowerInvariant();
        Name = validatedName;
        Slug = validatedSlug;
    }

    public string Name { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public OrganizationStatus Status { get; private set; } = OrganizationStatus.Active;

    public void Rename(string name)
    {
        if (Status == OrganizationStatus.Archived) throw new InvalidOperationException("An archived organization cannot be edited.");
        Name = DomainGuard.Text(name, 200, nameof(name));
    }

    public void ChangeStatus(OrganizationStatus status)
    {
        DomainGuard.Transition(Status, status, (Status, status) is
            (OrganizationStatus.Active, OrganizationStatus.Suspended or OrganizationStatus.Archived) or
            (OrganizationStatus.Suspended, OrganizationStatus.Active or OrganizationStatus.Archived));
        Status = status;
    }
}

public enum OrganizationStatus { Active, Suspended, Archived }
