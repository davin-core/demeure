using System.Net;
using System.Net.Http.Json;
using Demeure.Application.Modules.Reporting.Contracts;
using Demeure.Application.Modules.Users.Contracts;
using Demeure.Domain.Modules.Expenses.Entities;
using Demeure.Domain.Modules.Payments.Entities;
using Demeure.Domain.Modules.Units.Entities;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Infrastructure.Persistence;
using Demeure.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demeure.Tests.Integration;

public partial class AuthenticationTests
{
    [Fact]
    public async Task DashboardUsesRealScopedAmountsAndInclusiveUtcDates()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Owner);
        using (var scope = factory.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteDemeureDbContext>>();
            await using var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(owner.OrganizationId));
            var lease = await db.Leases.SingleAsync(value => value.Id == records.Resources[4].Assigned);
            lease.Activate();
            var payment = await db.Payments.SingleAsync(value => value.Id == records.Resources[5].Assigned);
            payment.Complete(new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero));
            var additional = new Payment(owner.OrganizationId, lease.Id, 125.45m, "USD", new DateOnly(2026, 2, 1));
            additional.Complete(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
            var nextMonth = new Payment(owner.OrganizationId, lease.Id, 9000, "CAD", new DateOnly(2026, 3, 1));
            nextMonth.Complete(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
            var refunded = new Payment(owner.OrganizationId, lease.Id, 7000, "CAD", new DateOnly(2026, 2, 1));
            refunded.Complete(new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero));
            refunded.Refund();
            var failed = new Payment(owner.OrganizationId, lease.Id, 200.25m, "CAD", new DateOnly(2025, 12, 1));
            failed.Fail();
            var cancelled = new Payment(owner.OrganizationId, lease.Id, 6000, "CAD", new DateOnly(2026, 2, 1));
            cancelled.Cancel();
            db.Payments.AddRange(additional, nextMonth, refunded, failed, cancelled);
            db.Expenses.AddRange(new Expense { OrganizationId = owner.OrganizationId, PropertyId = records.Resources[0].Assigned,
                Description = "Repair", Amount = 100.25m, Currency = "CAD", IncurredDate = new DateOnly(2026, 2, 28), Status = ExpenseStatus.Approved },
                new Expense { OrganizationId = owner.OrganizationId, PropertyId = records.Resources[0].Assigned,
                Description = "Draft", Amount = 9000, Currency = "CAD", IncurredDate = new DateOnly(2026, 2, 1), Status = ExpenseStatus.Draft });
            await db.SaveChangesAsync();
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var response = await client.GetAsync("/api/v1/dashboard?startDate=2026-02-01&endDate=2026-02-28");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var dashboard = (await response.Content.ReadFromJsonAsync<DashboardResponse>())!;
        Assert.Equal(2, dashboard.TotalProperties);
        Assert.Equal(2, dashboard.TotalUnits);
        Assert.Equal(2, dashboard.VacantUnits);
        Assert.Equal(0, dashboard.OccupancyRate);
        Assert.Equal(2, dashboard.PendingMaintenanceRequests);
        var cad = dashboard.Amounts.Single(value => value.Currency == "CAD");
        Assert.Equal(1000, cad.RentalIncome);
        Assert.Equal(1200.25m, cad.OutstandingRent);
        Assert.Equal(100.25m, cad.Expenses);
        Assert.Equal(125.45m, dashboard.Amounts.Single(value => value.Currency == "USD").RentalIncome);
        Assert.Single(dashboard.Months);
        Assert.Equal(1000, dashboard.Months[0].Amounts.Single(value => value.Currency == "CAD").RentalIncome);
        var expiry = (await client.GetFromJsonAsync<DashboardResponse>("/api/v1/dashboard?startDate=2026-12-01&endDate=2026-12-31"))!;
        Assert.Equal(1, expiry.ExpiringLeaseCount);
        Assert.Equal(records.Resources[4].Assigned, Assert.Single(expiry.ExpiringLeases).Id);
        var before = (await client.GetFromJsonAsync<DashboardResponse>("/api/v1/dashboard?startDate=2026-11-01&endDate=2026-11-30"))!;
        Assert.Equal(0, before.ExpiringLeaseCount);
        var boundary = (await client.GetFromJsonAsync<DashboardResponse>("/api/v1/dashboard?startDate=2026-12-01&endDate=2026-12-02"))!;
        Assert.Equal(1, boundary.ExpiringLeaseCount);
        var onEnd = (await client.GetFromJsonAsync<DashboardResponse>("/api/v1/dashboard?startDate=2027-01-01&endDate=2027-01-01"))!;
        Assert.Equal(0, onEnd.ExpiringLeaseCount);
    }

    [Theory]
    [InlineData(MembershipRole.Owner, 2)]
    [InlineData(MembershipRole.Manager, 1)]
    public async Task DashboardPropertyScopeCannotBeChangedByAnIdentifier(MembershipRole role, int expected)
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, role);
        using (var scope = factory.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteDemeureDbContext>>();
            await using var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(owner.OrganizationId));
            db.Expenses.AddRange(
                new Expense { OrganizationId = owner.OrganizationId, PropertyId = records.Resources[0].Assigned, Description = "Assigned repair", Amount = 200, Currency = "CAD", IncurredDate = new DateOnly(2026, 2, 1), Status = ExpenseStatus.Paid },
                new Expense { OrganizationId = owner.OrganizationId, PropertyId = records.Resources[0].Unassigned, Description = "Unassigned repair", Amount = 9000, Currency = "CAD", IncurredDate = new DateOnly(2026, 2, 1), Status = ExpenseStatus.Approved });
            await db.SaveChangesAsync();
            await db.Expenses.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.UpdatedAt, new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero)));
            await using var foreignDb = new SqliteDemeureDbContext(options, new AuthorizedTestContext(records.ForeignOrganizationId));
            foreignDb.Expenses.Add(new Expense { OrganizationId = records.ForeignOrganizationId, PropertyId = records.Resources[0].Foreign,
                Description = "Foreign confidential repair", Amount = 99000, Currency = "USD", IncurredDate = new DateOnly(2026, 2, 1), Status = ExpenseStatus.Paid });
            await foreignDb.SaveChangesAsync();
            await foreignDb.Expenses.ExecuteUpdateAsync(setters => setters.SetProperty(value => value.UpdatedAt, new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero)));
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        var dashboard = (await client.GetFromJsonAsync<DashboardResponse>("/api/v1/dashboard?startDate=2026-02-01&endDate=2026-02-28"))!;
        Assert.Equal(expected, dashboard.TotalProperties);
        Assert.Equal(expected, dashboard.PendingMaintenanceRequests);
        Assert.Equal(expected * 1000, Assert.Single(dashboard.Amounts).OutstandingRent);
        Assert.Equal(role == MembershipRole.Owner ? 9200 : 200, Assert.Single(dashboard.Amounts).Expenses);
        Assert.Equal(expected, dashboard.RecentActivities.Count);
        Assert.DoesNotContain(dashboard.RecentActivities, value => value.Label.Contains("Foreign"));
        if (role == MembershipRole.Manager) Assert.DoesNotContain(dashboard.RecentActivities, value => value.Label.Contains("Unassigned"));
        Assert.Equal(expected, dashboard.Properties.Count);
        Assert.DoesNotContain(dashboard.Properties, value => value.Id == records.Resources[0].Foreign);
        using var foreign = await client.GetAsync($"/api/v1/dashboard?propertyId={records.Resources[0].Foreign}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var unassigned = await client.GetAsync($"/api/v1/dashboard?propertyId={records.Resources[0].Unassigned}");
        Assert.Equal(role == MembershipRole.Owner ? HttpStatusCode.OK : HttpStatusCode.NotFound, unassigned.StatusCode);
        var filtered = (await client.GetFromJsonAsync<DashboardResponse>($"/api/v1/dashboard?propertyId={records.Resources[0].Assigned}"))!;
        Assert.Equal(1, filtered.TotalProperties);
        client.DefaultRequestHeaders.Add("X-Organization-Id", records.ForeignOrganizationId.ToString());
        using var changedOrg = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, changedOrg.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        if (role == MembershipRole.Manager)
        {
            using var scope = factory.Services.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteDemeureDbContext>>();
            await using var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(owner.OrganizationId));
            await db.PropertyManagerAssignments.ExecuteDeleteAsync();
            using var revoked = await client.GetAsync($"/api/v1/dashboard?propertyId={records.Resources[0].Assigned}");
            Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
            var noAssignments = (await client.GetFromJsonAsync<DashboardResponse>("/api/v1/dashboard"))!;
            Assert.Equal(0, noAssignments.TotalProperties);
            Assert.Empty(noAssignments.Amounts);
            Assert.Empty(noAssignments.RecentActivities);
        }
    }

    [Fact]
    public async Task DashboardDoesNotGrantPlatformAdministratorAnOrganizationBypass()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        await SeedAuthorizationAsync(factory, owner, MembershipRole.Member);
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityAccount>>();
            Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.SuperAdministrator))).Succeeded);
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync("owner@example.test"))!, ApplicationRoles.SuperAdministrator)).Succeeded);
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var response = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DashboardInventoryIncludesUnavailableButNotArchivedAndRevocationIsImmediate()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Owner);
        using (var scope = factory.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteDemeureDbContext>>();
            await using var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(owner.OrganizationId));
            (await db.Units.SingleAsync(value => value.Id == records.Resources[1].Assigned)).ChangeStatus(UnitStatus.Occupied);
            var unavailable = new Demeure.Domain.Modules.Units.Entities.Unit(owner.OrganizationId, records.Resources[0].Assigned, "Unavailable");
            unavailable.ChangeStatus(UnitStatus.Unavailable);
            var archived = new Demeure.Domain.Modules.Units.Entities.Unit(owner.OrganizationId, records.Resources[0].Assigned, "Archived");
            archived.ChangeStatus(UnitStatus.Archived);
            db.Units.AddRange(unavailable, archived);
            await db.SaveChangesAsync();
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        var response = (await client.GetFromJsonAsync<DashboardResponse>("/api/v1/dashboard"))!;
        Assert.Equal(3, response.TotalUnits);
        Assert.Equal(1, response.OccupiedUnits);
        Assert.Equal(1, response.VacantUnits);
        Assert.Equal(1, response.UnavailableUnits);
        Assert.Equal(33.33m, response.OccupancyRate);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
            await db.OrganizationMemberships.IgnoreQueryFilters().ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Status, MembershipStatus.Suspended));
        }
        using var denied = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Theory]
    [InlineData(MembershipRole.Tenant)]
    [InlineData(MembershipRole.Contractor)]
    [InlineData(MembershipRole.Member)]
    public async Task DashboardRejectsNonLandlordMemberships(MembershipRole role)
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        await SeedAuthorizationAsync(factory, owner, role);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var response = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("startDate=2026-02-30")]
    [InlineData("startDate=2026-03-01&endDate=2026-02-01")]
    [InlineData("startDate=2019-01-01&endDate=2026-02-01")]
    [InlineData("endDate=9999-12-31")]
    [InlineData("propertyId=00000000-0000-0000-0000-000000000000")]
    [InlineData("unknown=1")]
    [InlineData("startDate=2026-02-01&startDate=2026-02-02")]
    public async Task DashboardRejectsInvalidFilters(string query)
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var response = await client.GetAsync($"/api/v1/dashboard?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DashboardEmptyStateAliasesAndZeroFilledMonthsAreReal()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        using var anonymous = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        var empty = (await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard?startDate=2026-01-15&endDate=2026-03-02"))!;
        Assert.Equal(0, empty.TotalProperties);
        Assert.Equal(0, empty.TotalUnits);
        Assert.Null(empty.OccupancyRate);
        Assert.Empty(empty.Amounts);
        Assert.Empty(empty.RecentActivities);
        Assert.Equal(3, empty.Months.Count);
        Assert.All(empty.Months, value => Assert.Empty(value.Amounts));
        var fiveYears = (await client.GetFromJsonAsync<DashboardResponse>("/api/v1/dashboard?startDate=2024-01-01&endDate=2028-12-31"))!;
        Assert.Equal(60, fiveYears.Months.Count);
        Assert.All(fiveYears.Months, value => Assert.Empty(value.Amounts));
        using var beyondFiveYears = await client.GetAsync("/api/v1/dashboard?startDate=2024-01-01&endDate=2029-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, beyondFiveYears.StatusCode);
        using var leapBoundary = await client.GetAsync("/api/v1/dashboard?startDate=2024-02-29&endDate=2029-02-27");
        Assert.Equal(HttpStatusCode.OK, leapBoundary.StatusCode);
        using var beyondLeapBoundary = await client.GetAsync("/api/v1/dashboard?startDate=2024-02-29&endDate=2029-02-28");
        Assert.Equal(HttpStatusCode.BadRequest, beyondLeapBoundary.StatusCode);
    }
}
