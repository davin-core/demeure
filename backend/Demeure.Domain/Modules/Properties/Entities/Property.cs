using System.Diagnostics.CodeAnalysis;
using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Properties.Entities;

public sealed class Property : OrganizationEntity
{
    private Property() { }

    [SetsRequiredMembers]
    public Property(Guid organizationId, string name, string addressLine1, string city,
        string postalCode, string countryCode, string? addressLine2 = null, string? region = null)
    {
        OrganizationId = DomainGuard.Id(organizationId, nameof(organizationId));
        Rename(name);
        UpdateAddress(addressLine1, city, postalCode, countryCode, addressLine2, region);
    }

    public string Name { get; private set; } = "";
    public string AddressLine1 { get; private set; } = "";
    public string? AddressLine2 { get; private set; }
    public string City { get; private set; } = "";
    public string? Region { get; private set; }
    public string PostalCode { get; private set; } = "";
    public string CountryCode { get; private set; } = "";
    public PropertyStatus Status { get; private set; } = PropertyStatus.Active;

    public void Rename(string name)
    {
        EnsureEditable();
        Name = DomainGuard.Text(name, 200, nameof(name));
    }

    public void UpdateAddress(string addressLine1, string city, string postalCode,
        string countryCode, string? addressLine2 = null, string? region = null)
    {
        EnsureEditable();
        var line1 = DomainGuard.Text(addressLine1, 300, nameof(addressLine1));
        var line2 = DomainGuard.OptionalText(addressLine2, 300, nameof(addressLine2));
        var validatedCity = DomainGuard.Text(city, 100, nameof(city));
        var validatedRegion = DomainGuard.OptionalText(region, 100, nameof(region));
        var postal = DomainGuard.Text(postalCode, 20, nameof(postalCode));
        var country = DomainGuard.Text(countryCode, 2, nameof(countryCode)).ToUpperInvariant();
        if (country.Length != 2 || country.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Country must be a two-letter code.", nameof(countryCode));
        AddressLine1 = line1;
        AddressLine2 = line2;
        City = validatedCity;
        Region = validatedRegion;
        PostalCode = postal;
        CountryCode = country;
    }

    public void ChangeStatus(PropertyStatus status)
    {
        DomainGuard.Transition(Status, status, (Status, status) is
            (PropertyStatus.Active, PropertyStatus.Inactive or PropertyStatus.Archived) or
            (PropertyStatus.Inactive, PropertyStatus.Active or PropertyStatus.Archived));
        Status = status;
    }

    private void EnsureEditable()
    {
        if (Status == PropertyStatus.Archived) throw new InvalidOperationException("An archived property cannot be edited.");
    }
}

public enum PropertyStatus { Active, Inactive, Archived }
