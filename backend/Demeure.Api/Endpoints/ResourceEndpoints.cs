using Demeure.Application.Common.Authorization;
using Demeure.Api.Contracts;
using Demeure.Api.Extensions;

namespace Demeure.Api.Endpoints;

/// <summary>Maps read-only, organization-scoped resource endpoints.</summary>
public static class ResourceEndpoints
{
    /// <summary>Maps protected reads and explicitly audited platform access.</summary>
    public static IEndpointRouteBuilder MapResourceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var routes = new (string Path, ResourceKind Kind)[]
        {
            ("properties", ResourceKind.Property), ("units", ResourceKind.Unit), ("tenants", ResourceKind.Tenant),
            ("rental-applications", ResourceKind.RentalApplication), ("leases", ResourceKind.Lease),
            ("payments", ResourceKind.Payment), ("maintenance-requests", ResourceKind.Maintenance)
        };
        foreach (var (path, kind) in routes)
        {
            endpoints.MapGet($"/{path}", async ([AsParameters] CollectionParameters parameters,
                IResourceReader reader, CancellationToken cancellationToken) =>
                TypedResults.Ok(await reader.ListAsync(kind, parameters.ToQuery(), cancellationToken)))
                .RequireAuthorization(AuthorizationPolicies.OrganizationResource)
                .WithTags("Organization resources").WithName($"List{kind}")
                .WithSummary($"List authorized {kind} records with pagination, status filtering and deterministic sorting")
                .WithCollectionConventions();
            endpoints.MapGet($"/{path}/{{id:guid}}", async (Guid id, IResourceReader reader,
                CancellationToken cancellationToken) =>
                TypedResults.Ok(await reader.ReadAsync(kind, id, cancellationToken)))
                .RequireAuthorization(AuthorizationPolicies.OrganizationResource)
                .WithTags("Organization resources").WithName($"Read{kind}")
                .WithSummary($"Read an authorized {kind} record")
                .WithResourceConventions().ProducesProblem(404);
            endpoints.MapPost($"/platform/organizations/{{organizationId:guid}}/{path}/{{id:guid}}/access",
                async (Guid organizationId, Guid id, PlatformAccessRequest request, IResourceReader reader,
                    CancellationToken cancellationToken) =>
                    TypedResults.Ok(await reader.ReadPlatformAsync(organizationId, kind, id,
                        request.Reason, cancellationToken)))
                .RequireAuthorization(AuthorizationPolicies.PlatformResource)
                .RequireRateLimiting("auth-sensitive").WithTags("Platform access").WithName($"PlatformRead{kind}")
                .WithSummary($"Explicitly access a {kind} record with a mandatory audit reason")
                .WithResourceConventions().ProducesValidationProblem().ProducesProblem(404).ProducesProblem(429);
        }
        return endpoints;
    }
}
