using System.Diagnostics;
using System.Linq.Expressions;
using Demeure.Application.Common.Queries;
using Demeure.Application.Common.Authorization;
using Demeure.Application.Common.Exceptions;
using Demeure.Application.Modules.Users.Contracts;
using Demeure.Domain.Common;
using Demeure.Domain.Modules.Contractors.Entities;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Infrastructure.Authentication;
using Demeure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demeure.Infrastructure.Authorization;

internal sealed class ResourceReader(
    DemeureDbContext db, OrganizationContext context, IAuthenticationService authentication,
    OrganizationDataTransaction transactions, IHttpContextAccessor http,
    UserManager<IdentityAccount> users, ILogger<ResourceReader> logger) : IResourceReader
{
    public async Task<ResourceResponse> ReadAsync(ResourceKind kind, Guid id, CancellationToken cancellationToken)
    {
        var session = await authentication.GetSessionAsync(cancellationToken);
        var membership = session?.Memberships.SingleOrDefault(value => value.OrganizationId == context.OrganizationId);
        if (session is null || membership is null)
            throw new ForbiddenException("An active organization membership is required.");
        await using var transaction = await transactions.BeginAsync(session.UserId, false, cancellationToken);
        var result = await QueryAsync(kind, id, session.UserId, membership.Role, false, new CollectionQuery(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result.Items.SingleOrDefault() ?? throw new ResourceNotFoundException("The requested resource is unavailable.");
    }

    public async Task<PagedResponse<ResourceResponse>> ListAsync(ResourceKind kind, CollectionQuery query,
        CancellationToken cancellationToken)
    {
        query.Validate();
        var session = await authentication.GetSessionAsync(cancellationToken);
        var membership = session?.Memberships.SingleOrDefault(value => value.OrganizationId == context.OrganizationId);
        if (session is null || membership is null)
            throw new ForbiddenException("An active organization membership is required.");
        await using var transaction = await transactions.BeginAsync(session.UserId, false, cancellationToken, consistentSnapshot: true);
        var result = await QueryAsync(kind, null, session.UserId, membership.Role, false, query, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ResourceResponse> ReadPlatformAsync(Guid organizationId, ResourceKind kind, Guid id,
        string reason, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty || !Enum.IsDefined(kind) ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 10 or > 500)
            throw new RequestValidationException(new Dictionary<string, string[]> {
                ["access"] = ["A valid target and a reason of 10-500 characters are required."] });
        var account = http.HttpContext is { } request ? await users.GetUserAsync(request.User) : null;
        if (account is null || !await users.IsInRoleAsync(account, ApplicationRoles.SuperAdministrator) ||
            await authentication.GetSessionAsync(cancellationToken) is null)
            throw new ForbiddenException("Explicit platform access requires a super administrator.");
        if (!await db.Organizations.IgnoreQueryFilters().AnyAsync(value => value.Id == organizationId, cancellationToken))
            throw new ResourceNotFoundException("The requested organization is unavailable.");
        var previous = context.OrganizationId;
        try
        {
            context.Set(organizationId);
            await using var transaction = await transactions.BeginAsync(account.Id, true, cancellationToken);
            var result = await QueryAsync(kind, id, account.Id, "", true, new CollectionQuery(), cancellationToken);
            var resource = result.Items.SingleOrDefault();
            db.AuditLogs.Add(new AuditLog
            {
                OrganizationId = organizationId, PlatformActorUserId = account.Id,
                Action = resource is null ? "PlatformResourceReadNotFound" : "PlatformResourceRead",
                ResourceType = kind.ToString(), ResourceId = id, Reason = reason.Trim(),
                TraceId = Activity.Current?.TraceId.ToString() ?? http.HttpContext?.TraceIdentifier
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "Audited platform read by {UserId} in {TargetOrganizationId} for {ResourceType}/{ResourceId}; found {Found}",
                account.Id, organizationId, kind, id, resource is not null);
            return resource ?? throw new ResourceNotFoundException("The requested resource is unavailable.");
        }
        finally
        {
            context.Set(previous);
        }
    }

    private Task<PagedResponse<ResourceResponse>> QueryAsync(ResourceKind kind, Guid? id, Guid userId, string role,
        bool platform, CollectionQuery query, CancellationToken cancellationToken)
    {
        var owner = platform || role == ApplicationRoles.OrganizationOwner;
        var manager = role == ApplicationRoles.PropertyManager;
        var tenant = role == ApplicationRoles.Tenant;
        var contractor = role == ApplicationRoles.Contractor;
        var propertyIds = db.PropertyManagerAssignments.Where(value => value.UserId == userId).Select(value => value.PropertyId);
        var readableUnits = db.Units.Where(value => owner || manager && propertyIds.Contains(value.PropertyId)).Select(value => value.Id);
        var ownTenants = db.Tenants.Where(value => tenant && value.UserId == userId).Select(value => value.Id);
        var readableLeases = db.Leases.Where(value => owner ||
            manager && readableUnits.Contains(value.UnitId) || ownTenants.Contains(value.TenantId));
        var contractorIds = db.Contractors.Where(value => contractor && value.UserId == userId &&
            value.Status == ContractorStatus.Active).Select(value => value.Id);

        return kind switch
        {
            ResourceKind.Property => Execute(db.Properties
                .Where(value => owner || manager && propertyIds.Contains(value.Id)), value => value.Status,
                value => new PropertyResponse(value.Id, value.OrganizationId, value.Name, value.City, value.Status.ToString())),
            ResourceKind.Unit => Execute(db.Units
                .Where(value => owner || manager && propertyIds.Contains(value.PropertyId)), value => value.Status,
                value => new UnitResponse(value.Id, value.OrganizationId, value.PropertyId, value.Number, value.Status.ToString())),
            ResourceKind.Tenant => Execute(db.Tenants.Where(value =>
                (owner || ownTenants.Contains(value.Id) || manager &&
                    (db.Leases.Any(lease => lease.TenantId == value.Id && readableUnits.Contains(lease.UnitId)) ||
                     db.RentalApplications.Any(application => application.TenantId == value.Id && readableUnits.Contains(application.UnitId)) ||
                     db.MaintenanceRequests.Any(job => job.TenantId == value.Id && propertyIds.Contains(job.PropertyId))))), value => value.Status,
                value => new TenantResponse(value.Id, value.OrganizationId, value.FirstName, value.LastName, value.Status.ToString())),
            ResourceKind.RentalApplication => Execute(db.RentalApplications
                .Where(value => owner || manager && readableUnits.Contains(value.UnitId) || ownTenants.Contains(value.TenantId)), value => value.Status,
                value => new RentalApplicationResponse(value.Id, value.OrganizationId, value.TenantId, value.UnitId, value.Status.ToString())),
            ResourceKind.Lease => Execute(readableLeases, value => value.Status,
                value => new LeaseResponse(value.Id, value.OrganizationId, value.TenantId, value.UnitId,
                    value.StartDate, value.EndDate, value.MonthlyRent, value.Currency, value.Status.ToString())),
            ResourceKind.Payment => Execute(db.Payments
                .Where(value => readableLeases.Any(lease => lease.Id == value.LeaseId)), value => value.Status,
                value => new PaymentResponse(value.Id, value.OrganizationId, value.LeaseId, value.Amount, value.Currency, value.Status.ToString())),
            ResourceKind.Maintenance => Execute(db.MaintenanceRequests
                .Where(value => owner || manager && propertyIds.Contains(value.PropertyId) ||
                    value.TenantId.HasValue && ownTenants.Contains(value.TenantId.Value) ||
                    value.ContractorId.HasValue && contractorIds.Contains(value.ContractorId.Value)), value => value.Status,
                value => new MaintenanceResponse(value.Id, value.OrganizationId, value.PropertyId, value.TenantId,
                    value.ContractorId, value.Title, value.Description, value.Status.ToString())),
            _ => throw new RequestValidationException(new Dictionary<string, string[]> { ["resource"] = ["Unknown resource type."] })
        };

        Task<PagedResponse<ResourceResponse>> Execute<TEntity, TStatus>(IQueryable<TEntity> source,
            Expression<Func<TEntity, TStatus>> status, Expression<Func<TEntity, ResourceResponse>> projection)
            where TEntity : Entity where TStatus : struct, Enum =>
            QueryConventions.ReadAsync(source, status, projection, id, query, cancellationToken);
    }
}
