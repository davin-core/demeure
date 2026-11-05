using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Demeure.Application.Common.Authorization;
using Demeure.Application.Common.Queries;
using Demeure.Application.Modules.Users.Contracts;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Domain.Modules.Properties.Entities;
using Demeure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demeure.Tests.Integration;

public sealed partial class AuthenticationTests
{
    [Theory]
    [InlineData(MembershipRole.Owner)]
    [InlineData(MembershipRole.Administrator)]
    [InlineData(MembershipRole.Manager)]
    [InlineData(MembershipRole.Tenant)]
    [InlineData(MembershipRole.Contractor)]
    public async Task CollectionsPageOnlyAuthorizedRecordsAndNeverLeakForeignCounts(MembershipRole role)
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, role);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        foreach (var (path, assigned, unassigned, foreign) in records.Resources)
        {
            var count = role == MembershipRole.Owner ? 2 : 1;
            if (role == MembershipRole.Contractor && path != "maintenance-requests" ||
                role == MembershipRole.Tenant && path is "properties" or "units") count = 0;
            using var response = await client.GetAsync($"/api/v1/{path}?pageSize=1&sortBy=id&sortDirection=asc");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var page = (await response.Content.ReadFromJsonAsync<PagedResponse<ResourceResponse>>())!;
            Assert.Equal(count, page.TotalCount);
            Assert.Equal(count, page.TotalPages);
            Assert.Equal(1, page.PageSize);
            Assert.Equal(1, page.Page);
            Assert.Equal(count == 0 ? 0 : 1, page.Items.Count);
            Assert.All(page.Items, value => Assert.Equal(owner.OrganizationId, value.OrganizationId));
            Assert.DoesNotContain(page.Items, value => value.Id == foreign);
            if (role != MembershipRole.Owner) Assert.DoesNotContain(page.Items, value => value.Id == unassigned);
            if (count == 1) Assert.Equal(assigned, Assert.Single(page.Items).Id);
            using var second = await client.GetAsync($"/api/v1/{path}?page=2&pageSize=1");
            second.EnsureSuccessStatusCode();
            var next = (await second.Content.ReadFromJsonAsync<PagedResponse<ResourceResponse>>())!;
            Assert.Equal(count, next.TotalCount);
            if (count == 2) Assert.NotEqual(Assert.Single(page.Items).Id, Assert.Single(next.Items).Id);
            else Assert.Empty(next.Items);
        }
        client.DefaultRequestHeaders.Add("X-Organization-Id", records.ForeignOrganizationId.ToString());
        using var forged = await client.GetAsync("/api/v1/properties");
        Assert.Equal(HttpStatusCode.Forbidden, forged.StatusCode);
    }

    [Fact]
    public async Task CollectionsFilterBeforeCountingAndSortWithDeterministicIdTieBreakers()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        var records = await SeedAuthorizationAsync(factory, owner, MembershipRole.Owner);
        var property = records.Resources[0];
        using (var scope = factory.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteDemeureDbContext>>();
            await using var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(owner.OrganizationId));
            var inactive = await db.Properties.SingleAsync(value => value.Id == property.Assigned);
            inactive.ChangeStatus(PropertyStatus.Inactive);
            await db.SaveChangesAsync();
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        var active = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>("/api/v1/properties?status=Active");
        Assert.Equal(1, active!.TotalCount);
        Assert.Equal(property.Unassigned, Assert.Single(active.Items).Id);
        var inactivePage = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>("/api/v1/properties?status=Inactive");
        Assert.Equal(property.Assigned, Assert.Single(inactivePage!.Items).Id);
        var ascending = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>("/api/v1/properties?sortBy=status");
        var descending = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>("/api/v1/properties?sortBy=status&sortDirection=desc");
        Assert.Equal(property.Unassigned, ascending!.Items[0].Id);
        Assert.Equal(property.Assigned, descending!.Items[0].Id);
        foreach (var (path, _, _, _) in records.Resources)
        {
            var first = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>($"/api/v1/{path}?sortBy=createdAt&pageSize=1");
            var repeat = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>($"/api/v1/{path}?sortBy=createdAt&pageSize=1");
            Assert.Equal(Assert.Single(first!.Items).Id, Assert.Single(repeat!.Items).Id);
            Assert.Equal(2, first.TotalCount);
            using var byStatus = await client.GetAsync($"/api/v1/{path}?sortBy=status&sortDirection=desc");
            byStatus.EnsureSuccessStatusCode();
        }
    }

    [Theory]
    [InlineData("page=0", "Page")]
    [InlineData("pageSize=0", "PageSize")]
    [InlineData("pageSize=101", "PageSize")]
    [InlineData("page=2147483647&pageSize=100", "Page")]
    [InlineData("sortBy=passwordHash", "SortBy")]
    [InlineData("sortDirection=DESC", "SortDirection")]
    [InlineData("status=0", "status")]
    [InlineData("status=active", "status")]
    [InlineData("status=", "Status")]
    [InlineData("sortBy=", "SortBy")]
    [InlineData("unknown=true", "unknown")]
    [InlineData("page=1&page=2", "page")]
    public async Task InvalidCollectionQueriesReturnSafeFieldValidationProblems(string query, string field)
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var response = await client.GetAsync($"/api/v1/properties?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("/api/v1/properties", problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out var errors));
        Assert.NotEmpty(errors.EnumerateArray());
    }

    [Fact]
    public async Task CollectionDefaultsLimitsAndEmptyPagesHaveTheDocumentedShape()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        using (var scope = factory.Services.CreateScope())
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<SqliteDemeureDbContext>>();
            await using var db = new SqliteDemeureDbContext(options, new AuthorizedTestContext(owner.OrganizationId));
            for (var index = 0; index < 25; index++)
                db.Properties.Add(new Property(owner.OrganizationId, $"Property {index}", "Street", "City", "12345", "CA"));
            await db.SaveChangesAsync();
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        var defaults = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>("/api/v1/properties");
        Assert.Equal(20, defaults!.PageSize);
        Assert.Equal(1, defaults.Page);
        Assert.Equal(25, defaults.TotalCount);
        Assert.Equal(2, defaults.TotalPages);
        Assert.Equal(20, defaults.Items.Count);
        var maximum = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>("/api/v1/properties?pageSize=100&sortDirection=desc");
        Assert.Equal(25, maximum!.Items.Count);
        Assert.Equal(defaults.Items[0].Id, maximum.Items[^1].Id);
        var beyond = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>("/api/v1/properties?page=3");
        Assert.Equal(25, beyond!.TotalCount);
        Assert.Empty(beyond.Items);
        var noMatches = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>("/api/v1/properties?status=Archived");
        Assert.Equal(0, noMatches!.TotalCount);
        Assert.Equal(0, noMatches.TotalPages);
        Assert.Empty(noMatches.Items);
    }

    [Fact]
    public async Task EachResourceStatusFilterIsTypedAndMalformedNumbersAreRejected()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        await SeedAuthorizationAsync(factory, owner, MembershipRole.Owner);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        var statuses = new (string Path, string Status)[]
        {
            ("properties", "Active"), ("units", "Available"), ("tenants", "Active"),
            ("rental-applications", "Draft"), ("leases", "Draft"), ("payments", "Pending"),
            ("maintenance-requests", "Open")
        };
        foreach (var (path, status) in statuses)
        {
            var page = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>($"/api/v1/{path}?status={status}");
            Assert.Equal(2, page!.TotalCount);
            Assert.Equal(2, page.Items.Count);
            using var invalid = await client.GetAsync($"/api/v1/{path}?status=NotAStatus");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        foreach (var query in new[] { "page=not-a-number", "pageSize=2147483648", "page=", "sortDirection=" })
        {
            using var response = await client.GetAsync($"/api/v1/properties?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task CanonicalAndCompatibilityAuthRoutesShareCookiesAndPreserveLocationContracts()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf!.Token);
        using var registration = await client.PostAsJsonAsync("/api/v1/auth/register", Request());
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.Equal("/api/v1/auth/session", registration.Headers.Location?.ToString());
        var created = await registration.Content.ReadFromJsonAsync<RegistrationResponse>();
        using var verify = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = created!.UserId, Token = factory.Emails.Token("Verify") });
        verify.EnsureSuccessStatusCode();
        using var login = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = "owner@example.test", Password = Password });
        login.EnsureSuccessStatusCode();
        var current = await client.GetFromJsonAsync<SessionResponse>("/api/v1/auth/session");
        var legacy = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.Equal(current!.UserId, legacy!.UserId);
        var legacyPage = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>("/api/properties");
        var currentPage = await client.GetFromJsonAsync<PagedResponse<ResourceResponse>>("/api/v1/properties");
        Assert.Equal(legacyPage!.TotalCount, currentPage!.TotalCount);
        using var future = await client.GetAsync("/api/v2/properties");
        Assert.Equal(HttpStatusCode.NotFound, future.StatusCode);
    }
}
