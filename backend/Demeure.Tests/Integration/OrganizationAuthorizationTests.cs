using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Demeure.Application.Common;
using Demeure.Application.Common.Authorization;
using Demeure.Application.Modules.Users.Contracts;
using Demeure.Domain.Common;
using Demeure.Domain.Modules.Applications.Entities;
using Demeure.Domain.Modules.Contractors.Entities;
using Demeure.Domain.Modules.Leases.Entities;
using Demeure.Domain.Modules.Maintenance.Entities;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Domain.Modules.Payments.Entities;
using Demeure.Domain.Modules.Properties.Entities;
using Demeure.Domain.Modules.Tenants.Entities;
using Demeure.Infrastructure.Authentication;
using Demeure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using UnitEntity = Demeure.Domain.Modules.Units.Entities.Unit;

namespace Demeure.Tests.Integration;

public sealed partial class AuthenticationTests
{
    [Theory]
    [InlineData(MembershipRole.Owner)]
    [InlineData(MembershipRole.Manager)]
    [InlineData(MembershipRole.Administrator)]
    [InlineData(MembershipRole.Tenant)]
    [InlineData(MembershipRole.Contractor)]
    public async Task RealResourceIdentifiersRespectOrganizationAndResourceBoundaries(MembershipRole role)
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, role);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        foreach (var (path, assigned, unassigned, foreign) in records.Resources)
        {
            var canRead = role != MembershipRole.Contractor || path == "maintenance-requests";
            if (role == MembershipRole.Tenant && path is "properties" or "units") canRead = false;
            using var own = await client.GetAsync($"/api/{path}/{assigned}");
            Assert.Equal(canRead ? HttpStatusCode.OK : HttpStatusCode.NotFound, own.StatusCode);
            if (canRead)
            {
                var body = await own.Content.ReadFromJsonAsync<ResourceResponse>();
                Assert.NotNull(body);
                Assert.Equal(assigned, body.Id);
                Assert.Equal(owner.OrganizationId, body.OrganizationId);
                Assert.Equal("no-store", own.Headers.CacheControl?.ToString());
            }
            using var other = await client.GetAsync($"/api/{path}/{unassigned}");
            Assert.Equal(role == MembershipRole.Owner ? HttpStatusCode.OK : HttpStatusCode.NotFound, other.StatusCode);
            using var crossOrganization = await client.GetAsync($"/api/{path}/{foreign}");
            Assert.Equal(HttpStatusCode.NotFound, crossOrganization.StatusCode);
            Assert.Equal("application/problem+json", crossOrganization.Content.Headers.ContentType?.MediaType);
        }
        client.DefaultRequestHeaders.Add("X-Organization-Id", records.ForeignOrganizationId.ToString());
        using var forged = await client.GetAsync($"/api/properties/{records.Resources[0].Foreign}");
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
    }

    [Fact]
    public async Task MultipleAuthorizedMembershipsCanSwitchOrganizationsButCannotMixTheirIdentifiers()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Owner);
        using (var scope = factory.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteDemeureDbContext>>();
            await using var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(records.ForeignOrganizationId));
            db.OrganizationMemberships.Add(new OrganizationMembership(records.ForeignOrganizationId, owner.UserId, MembershipRole.Owner));
            await db.SaveChangesAsync();
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var ambiguous = await client.GetAsync($"/api/properties/{records.Resources[0].Assigned}");
        Assert.Equal(HttpStatusCode.Forbidden, ambiguous.StatusCode);
        client.DefaultRequestHeaders.Add("X-Organization-Id", owner.OrganizationId.ToString());
        using var first = await client.GetAsync($"/api/properties/{records.Resources[0].Assigned}");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var mixed = await client.GetAsync($"/api/properties/{records.Resources[0].Foreign}");
        Assert.Equal(HttpStatusCode.NotFound, mixed.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", records.ForeignOrganizationId.ToString());
        using var second = await client.GetAsync($"/api/properties/{records.Resources[0].Foreign}");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var reverse = await client.GetAsync($"/api/properties/{records.Resources[0].Assigned}");
        Assert.Equal(HttpStatusCode.NotFound, reverse.StatusCode);
    }

    [Fact]
    public async Task AssignmentForeignKeysRejectForeignPropertiesAndNonmemberUsers()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Manager);
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteDemeureDbContext>>();
        await using (var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(owner.OrganizationId)))
        {
            db.PropertyManagerAssignments.Add(new PropertyManagerAssignment(owner.OrganizationId,
                records.Resources[0].Foreign, owner.UserId));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(owner.OrganizationId)))
        {
            db.PropertyManagerAssignments.Add(new PropertyManagerAssignment(owner.OrganizationId,
                records.Resources[0].Assigned, Guid.NewGuid()));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(owner.OrganizationId)))
        {
            db.PropertyManagerAssignments.Add(new PropertyManagerAssignment(owner.OrganizationId,
                records.Resources[0].Assigned, owner.UserId));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task AssignmentAndContractorLinkRevocationTakeEffectOnTheNextRead()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Manager);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var assigned = await client.GetAsync($"/api/properties/{records.Resources[0].Assigned}");
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
            await db.PropertyManagerAssignments.IgnoreQueryFilters().ExecuteDeleteAsync();
        }
        using var removed = await client.GetAsync($"/api/properties/{records.Resources[0].Assigned}");
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
            await db.OrganizationMemberships.IgnoreQueryFilters().Where(value => value.UserId == owner.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Role, MembershipRole.Contractor));
        }
        var job = records.Resources.Single(value => value.Path == "maintenance-requests").Assigned;
        using var contractor = await client.GetAsync($"/api/maintenance-requests/{job}");
        Assert.Equal(HttpStatusCode.OK, contractor.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DemeureDbContext>().Contractors.IgnoreQueryFilters()
                .Where(value => value.UserId == owner.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.UserId, (Guid?)null));
        }
        using var unlinked = await client.GetAsync($"/api/maintenance-requests/{job}");
        Assert.Equal(HttpStatusCode.NotFound, unlinked.StatusCode);
    }

    [Fact]
    public async Task PlatformReadsRequireExplicitReasonAndAuditWithoutGrantingOrdinaryAccess()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Member);
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityAccount>>();
            Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.SuperAdministrator))).Succeeded);
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync("owner@example.test"))!,
                ApplicationRoles.SuperAdministrator)).Succeeded);
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        await CsrfAsync(client);
        var propertyId = records.Resources[0].Foreign;
        var target = $"/api/platform/organizations/{records.ForeignOrganizationId}/properties/{propertyId}/access";
        using var ordinary = await client.GetAsync($"/api/properties/{propertyId}");
        Assert.Equal(HttpStatusCode.Forbidden, ordinary.StatusCode);
        using var reasonMissing = await client.PostAsJsonAsync(target, new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, reasonMissing.StatusCode);
        using var accessed = await client.PostAsJsonAsync(target,
            new PlatformAccessRequest { Reason = "Investigating support incident 123" });
        Assert.Equal(HttpStatusCode.OK, accessed.StatusCode);
        var record = await accessed.Content.ReadFromJsonAsync<ResourceResponse>();
        Assert.Equal(records.ForeignOrganizationId, record!.OrganizationId);
        using var check = factory.Services.CreateScope();
        var audit = await check.ServiceProvider.GetRequiredService<DemeureDbContext>().AuditLogs.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(owner.UserId, audit.PlatformActorUserId);
        Assert.Null(audit.ActorUserId);
        Assert.Equal(propertyId, audit.ResourceId);
        Assert.Equal(records.ForeignOrganizationId, audit.OrganizationId);
        Assert.Equal("Investigating support incident 123", audit.Reason);
        Assert.Equal("PlatformResourceRead", audit.Action);
        Assert.NotNull(audit.TraceId);
        var contextAfter = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.Equal(owner.OrganizationId, Assert.Single(contextAfter!.Memberships).OrganizationId);
        using var stillDenied = await client.GetAsync($"/api/properties/{propertyId}");
        Assert.Equal(HttpStatusCode.Forbidden, stillDenied.StatusCode);
    }

    [Fact]
    public async Task MembershipAndOrganizationSuspensionDenyRealResources()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Owner);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DemeureDbContext>()
                .OrganizationMemberships.IgnoreQueryFilters().Where(value => value.UserId == owner.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Status, MembershipStatus.Suspended));
        }
        using var suspendedMember = await client.GetAsync($"/api/properties/{records.Resources[0].Assigned}");
        Assert.Equal(HttpStatusCode.Forbidden, suspendedMember.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
            await db.OrganizationMemberships.IgnoreQueryFilters().Where(value => value.UserId == owner.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Status, MembershipStatus.Active));
            await db.Organizations.IgnoreQueryFilters().Where(value => value.Id == owner.OrganizationId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Status, OrganizationStatus.Suspended));
        }
        using var suspendedOrganization = await client.GetAsync($"/api/properties/{records.Resources[0].Assigned}");
        Assert.Equal(HttpStatusCode.Forbidden, suspendedOrganization.StatusCode);
    }

    [Fact]
    public async Task MissingPlatformResourcesAreAuditedAndMissingCsrfCannotReadThem()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Member);
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityAccount>>();
            Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.SuperAdministrator))).Succeeded);
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync("owner@example.test"))!,
                ApplicationRoles.SuperAdministrator)).Succeeded);
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var id = Guid.NewGuid();
        var path = $"/api/platform/organizations/{records.ForeignOrganizationId}/properties/{id}/access";
        using var csrfDenied = await client.PostAsJsonAsync(path,
            new PlatformAccessRequest { Reason = "Investigating a nonexistent resource" });
        Assert.Equal(HttpStatusCode.BadRequest, csrfDenied.StatusCode);
        await CsrfAsync(client);
        using var missing = await client.PostAsJsonAsync(path,
            new PlatformAccessRequest { Reason = "Investigating a nonexistent resource" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var scopeAfter = factory.Services.CreateScope();
        var audit = await scopeAfter.ServiceProvider.GetRequiredService<DemeureDbContext>()
            .AuditLogs.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(id, audit.ResourceId);
        Assert.Equal("PlatformResourceReadNotFound", audit.Action);
    }

    [Fact]
    public async Task FailedAuditPersistenceNeverReturnsPlatformResourceData()
    {
        await using var factory = await AuthFactory.CreateAsync(rejectPlatformAudits: true);
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Member);
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityAccount>>();
            Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.SuperAdministrator))).Succeeded);
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync("owner@example.test"))!,
                ApplicationRoles.SuperAdministrator)).Succeeded);
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        await CsrfAsync(client);
        using var failed = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{records.ForeignOrganizationId}/properties/{records.Resources[0].Foreign}/access",
            new PlatformAccessRequest { Reason = "Investigating support incident with audit failure" });
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal("application/problem+json", failed.Content.Headers.ContentType?.MediaType);
        var problem = await failed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(500, problem.GetProperty("status").GetInt32());
        Assert.False(problem.TryGetProperty("resourceType", out _));
        Assert.False(problem.TryGetProperty("name", out _));
        using var scopeAfter = factory.Services.CreateScope();
        Assert.Empty(await scopeAfter.ServiceProvider.GetRequiredService<DemeureDbContext>()
            .AuditLogs.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task NonAdministratorsCannotUsePlatformAccessEvenWithOrganizationOwnership()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Owner);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        await CsrfAsync(client);
        using var denied = await client.PostAsJsonAsync(
            $"/api/platform/organizations/{records.ForeignOrganizationId}/properties/{records.Resources[0].Foreign}/access",
            new PlatformAccessRequest { Reason = "I should not have platform access" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var anonymous = factory.Client();
        using var protectedRead = await anonymous.GetAsync($"/api/properties/{records.Resources[0].Assigned}");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedRead.StatusCode);
    }

    private static async Task<AuthorizationRecords> SeedAuthorizationAsync(AuthFactory factory,
        RegistrationResponse owner, MembershipRole role)
    {
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteDemeureDbContext>>();
        var organization = new Organization("Other organization", $"other-{Guid.NewGuid():N}");
        await using var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(owner.OrganizationId));
        var first = AddResourceSet(db, owner.OrganizationId, owner.UserId, "A");
        var second = AddResourceSet(db, owner.OrganizationId, null, "B");
        db.PropertyManagerAssignments.Add(new PropertyManagerAssignment(owner.OrganizationId, first.Property, owner.UserId));
        await db.SaveChangesAsync();
        await db.OrganizationMemberships.IgnoreQueryFilters().Where(value => value.UserId == owner.UserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Role, role));
        await using var foreignDb = new SqliteDemeureDbContext(options, new AuthorizedTestContext(organization.Id));
        foreignDb.Organizations.Add(organization);
        var foreign = AddResourceSet(foreignDb, organization.Id, null, "C");
        await foreignDb.SaveChangesAsync();
        return new AuthorizationRecords(organization.Id,
        [
            ("properties", first.Property, second.Property, foreign.Property),
            ("units", first.Unit, second.Unit, foreign.Unit),
            ("tenants", first.Tenant, second.Tenant, foreign.Tenant),
            ("rental-applications", first.Application, second.Application, foreign.Application),
            ("leases", first.Lease, second.Lease, foreign.Lease),
            ("payments", first.Payment, second.Payment, foreign.Payment),
            ("maintenance-requests", first.Job, second.Job, foreign.Job)
        ]);
    }

    private static ResourceSet AddResourceSet(DemeureDbContext db, Guid organizationId, Guid? userId, string name)
    {
        var property = new Property(organizationId, name, "1 Main Street", "Montreal", "H1A1A1", "CA");
        var unit = new UnitEntity(organizationId, property.Id, name, 1, 1);
        var tenant = new Tenant(organizationId, name, "Resident", userId);
        var application = new RentalApplication { OrganizationId = organizationId, UnitId = unit.Id, TenantId = tenant.Id };
        var lease = new Lease(organizationId, unit.Id, tenant.Id, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1),
            1000, 1000, "CAD", application.Id);
        var payment = new Payment(organizationId, lease.Id, 1000, "CAD", new DateOnly(2026, 2, 1));
        var contractor = new Contractor { OrganizationId = organizationId, Name = name, UserId = userId };
        var job = new MaintenanceRequest
        {
            OrganizationId = organizationId, PropertyId = property.Id, UnitId = unit.Id,
            TenantId = tenant.Id, ContractorId = contractor.Id, Title = name, Description = "Repair sink"
        };
        db.AddRange(property, unit, tenant, application, lease, payment, contractor, job);
        return new ResourceSet(property.Id, unit.Id, tenant.Id, application.Id, lease.Id, payment.Id, job.Id);
    }

    private sealed record ResourceSet(Guid Property, Guid Unit, Guid Tenant, Guid Application, Guid Lease, Guid Payment, Guid Job);
    private sealed record AuthorizationRecords(Guid ForeignOrganizationId,
        (string Path, Guid Assigned, Guid Unassigned, Guid Foreign)[] Resources);
    private sealed record AuthorizedTestContext(Guid? OrganizationId) : IOrganizationContext;

    private sealed class RejectPlatformAudits : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLog>()
                .Any(entry => entry.State == EntityState.Added && entry.Entity.PlatformActorUserId.HasValue))
                throw new IOException("Deliberate test audit persistence failure.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
