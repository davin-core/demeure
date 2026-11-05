using System.Diagnostics.CodeAnalysis;
using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Units.Entities;

public sealed class Unit : OrganizationEntity
{
    private Unit() { }

    [SetsRequiredMembers]
    public Unit(Guid organizationId, Guid propertyId, string number, int bedrooms = 0, int bathrooms = 0)
    {
        OrganizationId = DomainGuard.Id(organizationId, nameof(organizationId));
        PropertyId = DomainGuard.Id(propertyId, nameof(propertyId));
        UpdateDetails(number, bedrooms, bathrooms);
    }

    public Guid PropertyId { get; private set; }
    public string Number { get; private set; } = "";
    public int Bedrooms { get; private set; }
    public int Bathrooms { get; private set; }
    public UnitStatus Status { get; private set; } = UnitStatus.Available;

    public void UpdateDetails(string number, int bedrooms, int bathrooms)
    {
        if (Status == UnitStatus.Archived) throw new InvalidOperationException("An archived unit cannot be edited.");
        var validatedNumber = DomainGuard.Text(number, 50, nameof(number));
        if (bedrooms < 0) throw new ArgumentOutOfRangeException(nameof(bedrooms));
        if (bathrooms < 0) throw new ArgumentOutOfRangeException(nameof(bathrooms));
        Number = validatedNumber;
        Bedrooms = bedrooms;
        Bathrooms = bathrooms;
    }

    public void ChangeStatus(UnitStatus status)
    {
        DomainGuard.Transition(Status, status, (Status, status) is
            (UnitStatus.Available, UnitStatus.Occupied or UnitStatus.Unavailable or UnitStatus.Archived) or
            (UnitStatus.Occupied, UnitStatus.Available) or
            (UnitStatus.Unavailable, UnitStatus.Available or UnitStatus.Archived));
        Status = status;
    }
}

public enum UnitStatus { Available, Occupied, Unavailable, Archived }
