using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Demeure.Application.Common.Queries;

namespace Demeure.Application.Common.Authorization;

/// <summary>Supported protected read resources; not a permission granted by the client.</summary>
public enum ResourceKind { Property, Unit, Tenant, RentalApplication, Lease, Payment, Maintenance }

/// <summary>Discriminated read-only DTO contract shared by organization and platform reads.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "resourceType")]
[JsonDerivedType(typeof(PropertyResponse), "property")]
[JsonDerivedType(typeof(UnitResponse), "unit")]
[JsonDerivedType(typeof(TenantResponse), "tenant")]
[JsonDerivedType(typeof(RentalApplicationResponse), "rentalApplication")]
[JsonDerivedType(typeof(LeaseResponse), "lease")]
[JsonDerivedType(typeof(PaymentResponse), "payment")]
[JsonDerivedType(typeof(MaintenanceResponse), "maintenance")]
public abstract record ResourceResponse(Guid Id, Guid OrganizationId);
/// <summary>An authorized property's summary, without resident data.</summary>
public sealed record PropertyResponse(Guid Id, Guid OrganizationId, string Name, string City, string Status)
    : ResourceResponse(Id, OrganizationId);
/// <summary>An authorized unit's summary.</summary>
public sealed record UnitResponse(Guid Id, Guid OrganizationId, Guid PropertyId, string Number, string Status)
    : ResourceResponse(Id, OrganizationId);
/// <summary>A resident profile available to the resident or authorized property staff.</summary>
public sealed record TenantResponse(Guid Id, Guid OrganizationId, string FirstName, string LastName, string Status)
    : ResourceResponse(Id, OrganizationId);
/// <summary>An authorized rental application's tenancy links and status.</summary>
public sealed record RentalApplicationResponse(Guid Id, Guid OrganizationId, Guid TenantId, Guid UnitId, string Status)
    : ResourceResponse(Id, OrganizationId);
/// <summary>An authorized lease; dates are calendar dates rather than timestamps.</summary>
public sealed record LeaseResponse(Guid Id, Guid OrganizationId, Guid TenantId, Guid UnitId,
    DateOnly StartDate, DateOnly EndDate, decimal MonthlyRent, string Currency, string Status)
    : ResourceResponse(Id, OrganizationId);
/// <summary>An authorized lease payment without provider credentials.</summary>
public sealed record PaymentResponse(Guid Id, Guid OrganizationId, Guid LeaseId, decimal Amount, string Currency, string Status)
    : ResourceResponse(Id, OrganizationId);
/// <summary>A maintenance job visible to authorized property staff, its resident or assigned contractor.</summary>
public sealed record MaintenanceResponse(Guid Id, Guid OrganizationId, Guid PropertyId, Guid? TenantId,
    Guid? ContractorId, string Title, string Description, string Status) : ResourceResponse(Id, OrganizationId);

/// <summary>Mandatory justification for an explicitly audited platform read.</summary>
public sealed record PlatformAccessRequest
{
    [Required, MinLength(10), MaxLength(500)]
    public required string Reason { get; init; }
}

/// <summary>Enforces membership and resource scope before projecting any record into a DTO.</summary>
public interface IResourceReader
{
    Task<PagedResponse<ResourceResponse>> ListAsync(ResourceKind kind, CollectionQuery query, CancellationToken cancellationToken);
    Task<ResourceResponse> ReadAsync(ResourceKind kind, Guid id, CancellationToken cancellationToken);
    Task<ResourceResponse> ReadPlatformAsync(Guid organizationId, ResourceKind kind, Guid id,
        string reason, CancellationToken cancellationToken);
}

public static class AuthorizationPolicies
{
    public const string OrganizationResource = "OrganizationResource";
    public const string PlatformResource = "PlatformResource";
}
