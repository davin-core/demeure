using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Demeure.Application.Common;
using Demeure.Application.Modules.Notifications.Contracts;
using Demeure.Application.Modules.Users.Contracts;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Domain.Modules.Users.Entities;
using Demeure.Infrastructure.Authentication;
using Demeure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Demeure.Tests.Integration;

public sealed partial class AuthenticationTests
{
    private const string Password = "Correct-Horse-482!";

    [Fact]
    public async Task MultipleMembershipsRequireSelectionAndRolesCannotLeakBetweenRequests()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var owner = await RegisterAndVerifyAsync(factory, client);
        using var other = factory.Client();
        await CsrfAsync(other);
        using var registered = await other.PostAsJsonAsync("/api/auth/register",
            Request("other@example.test", "other-organization"));
        registered.EnsureSuccessStatusCode();
        var organization = await registered.Content.ReadFromJsonAsync<RegistrationResponse>();
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DemeureDbContext>()
                .OrganizationMemberships.IgnoreQueryFilters()
                .Where(value => value.OrganizationId == organization!.OrganizationId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(value => value.UserId, owner.UserId)
                    .SetProperty(value => value.Role, MembershipRole.Manager));
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var ambiguous = await client.GetAsync("/__tests/owner");
        Assert.Equal(HttpStatusCode.Forbidden, ambiguous.StatusCode);
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.Equal(2, session!.Memberships.Count);
        client.DefaultRequestHeaders.Add("X-Organization-Id", owner.OrganizationId.ToString());
        using var owns = await client.GetAsync("/__tests/owner");
        Assert.Equal(HttpStatusCode.OK, owns.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", organization!.OrganizationId.ToString());
        using var manages = await client.GetAsync($"/__tests/role/{ApplicationRoles.PropertyManager}");
        using var notOwner = await client.GetAsync("/__tests/owner");
        Assert.Equal(HttpStatusCode.OK, manages.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notOwner.StatusCode);
    }

    [Fact]
    public async Task ExpiredResetTokensCannotChangeThePassword()
    {
        await using var factory = await AuthFactory.CreateAsync(tokenLifetime: TimeSpan.FromTicks(-1));
        using var client = factory.Client();
        await RegisterAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityAccount>>();
            var account = (await users.FindByEmailAsync("owner@example.test"))!;
            account.EmailConfirmed = true;
            Assert.True((await users.UpdateAsync(account)).Succeeded);
        }
        using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new EmailRequest { Email = "owner@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        using var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = "owner@example.test", Token = factory.Emails.Token("password reset"), NewPassword = "OtherStrongPass!456"
        });
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        using var check = factory.Services.CreateScope();
        var manager = check.ServiceProvider.GetRequiredService<UserManager<IdentityAccount>>();
        Assert.True(await manager.CheckPasswordAsync((await manager.FindByEmailAsync("owner@example.test"))!, Password));
    }

    [Fact]
    public async Task StampValidationDoesNotRenewFixedSessionsAndExpiredCookiesAreRejected()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("Demeure.Session=", StringComparison.Ordinal));
        var encoded = cookie.Split(';')[0]["Demeure.Session=".Length..];
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var ticket = options.TicketDataFormat.Unprotect(encoded);
        Assert.NotNull(ticket);
        Assert.Equal(TimeSpan.FromHours(8), ticket.Properties.ExpiresUtc - ticket.Properties.IssuedUtc);
        using var session = await client.GetAsync("/api/auth/session");
        session.EnsureSuccessStatusCode();
        Assert.False(session.Headers.TryGetValues("Set-Cookie", out var renewed) &&
            renewed.Any(value => value.StartsWith("Demeure.Session=", StringComparison.Ordinal)));
        ticket.Properties.IssuedUtc = DateTimeOffset.UtcNow.AddHours(-9);
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(-1);
        using var expired = factory.Client();
        expired.DefaultRequestHeaders.Add("Cookie", $"Demeure.Session={options.TicketDataFormat.Protect(ticket)}");
        using var rejected = await expired.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    [Fact]
    public async Task EmailFailureIsExplicitAndCommittedRegistrationCanResendVerification()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await CsrfAsync(client);
        factory.Emails.FailDelivery = true;
        using var registration = await client.PostAsJsonAsync("/api/auth/register", Request());
        Assert.Equal(HttpStatusCode.InternalServerError, registration.StatusCode);
        Assert.Equal("application/problem+json", registration.Content.Headers.ContentType?.MediaType);
        using var scope = factory.Services.CreateScope();
        var account = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityAccount>>()
            .FindByEmailAsync("owner@example.test");
        Assert.NotNull(account);
        factory.Emails.FailDelivery = false;
        using var resend = await client.PostAsJsonAsync("/api/auth/verification-email",
            new EmailRequest { Email = "owner@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        using var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = account.Id, Token = factory.Emails.Token("Verify") });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        using var login = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }

    [Fact]
    public async Task RegistrationDtoValidationReturnsFieldErrorsBeforeCreatingRecords()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await CsrfAsync(client);
        using var response = await client.PostAsJsonAsync("/api/auth/register", Request(email: "not-an-email"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.Contains(problem.GetProperty("errors").EnumerateObject(), field =>
            field.Name.EndsWith("Email", StringComparison.OrdinalIgnoreCase) && field.Value.GetArrayLength() > 0);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<DemeureDbContext>().Users.ToListAsync());
    }

    [Fact]
    public async Task AnotherRegisteredOrganizationDoesNotGrantOwnerAccess()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var owner = factory.Client();
        await RegisterAndVerifyAsync(factory, owner);
        using var other = factory.Client();
        await CsrfAsync(other);
        using var registration = await other.PostAsJsonAsync("/api/auth/register",
            Request("other-owner@example.test", "other-organization"));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var created = await registration.Content.ReadFromJsonAsync<RegistrationResponse>();
        using var login = await LoginAsync(owner);
        login.EnsureSuccessStatusCode();
        owner.DefaultRequestHeaders.Add("X-Organization-Id", created!.OrganizationId.ToString());
        using var forbidden = await owner.GetAsync("/__tests/owner");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task RegistrationCannotSelfAssignRolesOrOwnAnExistingIdentifier()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await CsrfAsync(client);
        var suppliedId = Guid.NewGuid();
        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "owner@example.test", password = Password, displayName = "Owner",
            organizationName = "Organization", organizationSlug = "organization",
            role = ApplicationRoles.SuperAdministrator, organizationId = suppliedId
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotEqual(suppliedId, created!.OrganizationId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
        Assert.Empty(await db.Set<IdentityUserRole<Guid>>().ToListAsync());
        Assert.Equal(MembershipRole.Owner,
            (await db.OrganizationMemberships.IgnoreQueryFilters().SingleAsync()).Role);
    }

    [Fact]
    public async Task SuperAdministratorIsOnlyGrantedByTrustedIdentityProvisioning()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await RegisterAndVerifyAsync(factory, client);
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
        using var admin = await client.GetAsync("/__tests/admin");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.Equal(ApplicationRoles.SuperAdministrator, Assert.Single(session!.GlobalRoles));
    }

    [Fact]
    public async Task GloballyStoredOrganizationRolesCannotBypassMembershipAuthorization()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await RegisterAndVerifyAsync(factory, client);
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityAccount>>();
            Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.OrganizationOwner))).Succeeded);
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync("owner@example.test"))!,
                ApplicationRoles.OrganizationOwner)).Succeeded);
            await scope.ServiceProvider.GetRequiredService<DemeureDbContext>().OrganizationMemberships.IgnoreQueryFilters()
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Status, MembershipStatus.Removed));
        }
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var owner = await client.GetAsync("/__tests/owner");
        Assert.Equal(HttpStatusCode.Forbidden, owner.StatusCode);
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.Empty(session!.GlobalRoles);
        Assert.Empty(session.Memberships);
    }

    [Fact]
    public async Task DuplicateEmailOrSlugCannotCreateOrTransferOwnership()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await RegisterAsync(client);
        using var duplicateEmail = await client.PostAsJsonAsync("/api/auth/register", Request(slug: "other-slug"));
        using var duplicateSlug = await client.PostAsJsonAsync("/api/auth/register", Request(email: "other@example.test"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateEmail.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateSlug.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
        Assert.Single(await db.Users.ToListAsync());
        Assert.Single(await db.Organizations.IgnoreQueryFilters().ToListAsync());
        Assert.Single(await db.OrganizationMemberships.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task ExpiredConfirmationTokensAreRejected()
    {
        await using var factory = await AuthFactory.CreateAsync(tokenLifetime: TimeSpan.FromTicks(-1));
        using var client = factory.Client();
        var registration = await RegisterAsync(client);
        using var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = registration.UserId, Token = factory.Emails.Token("Verify") });
        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
    }

    [Fact]
    public async Task ProductionCookiesAreSecureAndNeverRedirectApiFailures()
    {
        await using var factory = await AuthFactory.CreateAsync(environment: "Production");
        using var client = factory.Client();
        await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("Demeure.Session=", StringComparison.Ordinal));
        Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase);
        using var admin = await client.GetAsync("/__tests/admin");
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.Equal("application/problem+json", admin.Content.Headers.ContentType?.MediaType);
        Assert.False(admin.Headers.Contains("Location"));
    }

    [Fact]
    public async Task RecoveryRejectsMalformedTokensWithoutLoggingAProgrammingFailure()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var registration = await RegisterAndVerifyAsync(factory, client);
        using var confirm = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = registration.UserId, Token = "!bad-token!" });
        using var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = "owner@example.test", Token = "!bad-token!", NewPassword = Password });
        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task RegistrationPersistsAnOwnerAndPasswordHashButDoesNotSignIn()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var registration = await RegisterAsync(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
        var user = await db.Users.SingleAsync();
        var membership = await db.OrganizationMemberships.IgnoreQueryFilters().SingleAsync();
        var account = await db.Set<IdentityAccount>().SingleAsync();

        Assert.Equal(user.Id, account.Id);
        Assert.Equal(registration.UserId, membership.UserId);
        Assert.Equal(registration.OrganizationId, membership.OrganizationId);
        Assert.Equal(MembershipRole.Owner, membership.Role);
        Assert.False(account.EmailConfirmed);
        Assert.NotNull(account.PasswordHash);
        Assert.DoesNotContain(Password, account.PasswordHash);
        Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<IdentityAccount>>().CheckPasswordAsync(account, Password));
        Assert.Empty(await db.Set<IdentityUserRole<Guid>>().ToListAsync());
        Assert.Single(factory.Emails.Messages);
        using var session = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
    }

    [Fact]
    public async Task WeakPasswordRollsBackTheOrganizationAndProfile()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await CsrfAsync(client);
        using var response = await client.PostAsJsonAsync("/api/auth/register", Request(password: "abcdefghijklmnop"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Organizations.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.OrganizationMemberships.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(factory.Emails.Messages);
    }

    [Fact]
    public async Task VerificationIsRequiredAndTokensArePurposeBoundAndNotReusable()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var registration = await RegisterAsync(client);
        using var login = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var token = factory.Emails.Token("Verify");
        using var wrong = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = Guid.NewGuid(), Token = token });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        using var confirmed = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = registration.UserId, Token = token });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        using var repeated = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = registration.UserId, Token = token });
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        using var wrongPurpose = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Email = "owner@example.test", Token = token, NewPassword = "Different-Pass-482!" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
        using var success = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, success.StatusCode);
    }

    [Fact]
    public async Task LoginIssuesHttpOnlyStrictCookieAndSessionDescribesOwnedOrganization()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var registration = await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), value => value.StartsWith("Demeure.Session=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, cookie);
        Assert.DoesNotContain("owner@example.test", cookie);
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.NotNull(session);
        Assert.Empty(session.GlobalRoles);
        Assert.Equal(ApplicationRoles.OrganizationOwner, Assert.Single(session.Memberships).Role);
        Assert.Equal(registration.OrganizationId, session.Memberships[0].OrganizationId);
        using var owner = await client.GetAsync("/__tests/owner");
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        using var admin = await client.GetAsync("/__tests/admin");
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        Assert.False(admin.Headers.Contains("Location"));
    }

    [Fact]
    public async Task CsrfRejectsMissingInvalidAndPreLoginTokens()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        using var missing = await client.PostAsJsonAsync("/api/auth/register", Request());
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid");
        using var invalid = await client.PostAsJsonAsync("/api/auth/register", Request());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var stale = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        await CsrfAsync(client);
        using var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var session = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
    }

    [Fact]
    public async Task OwnerRoleDoesNotAuthorizeAnotherOrganization()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("X-Organization-Id", Guid.NewGuid().ToString());
        using var response = await client.GetAsync("/__tests/owner");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", "not-a-guid");
        using var invalid = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Theory]
    [InlineData(MembershipRole.Manager, ApplicationRoles.PropertyManager)]
    [InlineData(MembershipRole.Tenant, ApplicationRoles.Tenant)]
    [InlineData(MembershipRole.Contractor, ApplicationRoles.Contractor)]
    public async Task OrganizationRolesComeFromCurrentMembershipNotTheCookie(MembershipRole role, string expectedRole)
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var registration = await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
            var membership = await db.OrganizationMemberships.IgnoreQueryFilters().SingleAsync();
            membership.ChangeRole(role);
            // Tests deliberately bypass tracked organization guards to simulate an authorized administrative change.
            await db.OrganizationMemberships.IgnoreQueryFilters().Where(value => value.Id == membership.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Role, role));
        }
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.Equal(expectedRole, Assert.Single(session!.Memberships).Role);
        Assert.Equal(registration.OrganizationId, session.Memberships[0].OrganizationId);
        using var owner = await client.GetAsync("/__tests/owner");
        Assert.Equal(HttpStatusCode.Forbidden, owner.StatusCode);
        using var allowed = await client.GetAsync($"/__tests/role/{expectedRole}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task SuspendedMembershipLosesOrganizationPermissionsImmediately()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        var registration = await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
            await db.OrganizationMemberships.IgnoreQueryFilters()
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Status, MembershipStatus.Suspended));
        }
        using var owner = await client.GetAsync("/__tests/owner");
        Assert.Equal(HttpStatusCode.Forbidden, owner.StatusCode);
        client.DefaultRequestHeaders.Add("X-Organization-Id", registration.OrganizationId.ToString());
        using var explicitOrg = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Forbidden, explicitOrg.StatusCode);
    }

    [Fact]
    public async Task FiveIncorrectPasswordsLockOutTheAccount()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await RegisterAndVerifyAsync(factory, client);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var wrong = await LoginAsync(client, "Incorrect-Password-12!");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }
        using var locked = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityAccount>>();
        var account = await manager.FindByEmailAsync("owner@example.test");
        Assert.True(await manager.IsLockedOutAsync(account!));
    }

    [Fact]
    public async Task RecoveryRequestsDoNotRevealAccountExistence()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await CsrfAsync(client);
        using var reset = await client.PostAsJsonAsync("/api/auth/forgot-password", new EmailRequest { Email = "missing@example.test" });
        using var verify = await client.PostAsJsonAsync("/api/auth/verification-email", new EmailRequest { Email = "missing@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, verify.StatusCode);
        Assert.Empty(factory.Emails.Messages);
    }

    [Fact]
    public async Task PasswordResetRevokesSessionsAndCannotReuseTheToken()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using var recovery = factory.Client();
        await CsrfAsync(recovery);
        using var email = await recovery.PostAsJsonAsync("/api/auth/forgot-password", new EmailRequest { Email = "owner@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, email.StatusCode);
        var token = factory.Emails.Token("password reset");
        var request = new ResetPasswordRequest { Email = "owner@example.test", Token = token, NewPassword = "New-Correct-Horse-482!" };
        using var reset = await recovery.PostAsJsonAsync("/api/auth/reset-password", request);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        using var oldSession = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        using var reuse = await recovery.PostAsJsonAsync("/api/auth/reset-password", request);
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);
        using var oldLogin = await LoginAsync(recovery);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        using var newLogin = await LoginAsync(recovery, request.NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, newLogin.StatusCode);
    }

    [Fact]
    public async Task LogoutAllRevokesOtherBrowserSessions()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var first = factory.Client();
        await RegisterAndVerifyAsync(factory, first);
        using var login = await LoginAsync(first);
        login.EnsureSuccessStatusCode();
        using var second = factory.Client();
        await CsrfAsync(second);
        using var secondLogin = await LoginAsync(second);
        secondLogin.EnsureSuccessStatusCode();
        await CsrfAsync(first);
        using var logout = await first.PostAsync("/api/auth/logout-all", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var secondSession = await second.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, secondSession.StatusCode);
    }

    [Fact]
    public async Task DisabledDomainUsersCannotKeepOrCreateSessions()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await RegisterAndVerifyAsync(factory, client);
        using var login = await LoginAsync(client);
        login.EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();
            var user = await db.Users.SingleAsync();
            user.ChangeStatus(UserStatus.Disabled);
            await db.SaveChangesAsync();
        }
        using var session = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        await CsrfAsync(client);
        using var disabledLogin = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, disabledLogin.StatusCode);
    }

    [Fact]
    public async Task RateLimitReturnsCorrelated429WithRetryAfter()
    {
        await using var factory = await AuthFactory.CreateAsync();
        using var client = factory.Client();
        await CsrfAsync(client);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var accepted = await client.PostAsJsonAsync("/api/auth/forgot-password",
                new EmailRequest { Email = "missing@example.test" });
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }
        using var rejected = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new EmailRequest { Email = "missing@example.test" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.True(rejected.Headers.Contains("Retry-After"));
        Assert.Contains("traceId", await rejected.Content.ReadAsStringAsync());
    }

    private static RegisterRequest Request(string email = "owner@example.test", string slug = "test-organization",
        string password = Password) => new()
    {
        Email = email, Password = password, DisplayName = "Organization owner",
        OrganizationName = "Test organization", OrganizationSlug = slug
    };

    private static async Task CsrfAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", response!.Token);
    }

    private static async Task<RegistrationResponse> RegisterAsync(HttpClient client)
    {
        await CsrfAsync(client);
        using var response = await client.PostAsJsonAsync("/api/auth/register", Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegistrationResponse>())!;
    }

    private static async Task<RegistrationResponse> RegisterAndVerifyAsync(AuthFactory factory, HttpClient client)
    {
        var registration = await RegisterAsync(client);
        using var confirmed = await client.PostAsJsonAsync("/api/auth/confirm-email",
            new ConfirmEmailRequest { UserId = registration.UserId, Token = factory.Emails.Token("Verify") });
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        return registration;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "owner@example.test", Password = password });

    private sealed class RecordingEmails : IAccountEmailSender
    {
        public bool FailDelivery { get; set; }
        public ConcurrentQueue<(string Recipient, string Subject, string Body)> Messages { get; } = new();
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
        {
            if (FailDelivery) throw new System.Net.Mail.SmtpException("Deliberate test delivery failure.");
            Messages.Enqueue((recipient, subject, body));
            return Task.CompletedTask;
        }
        public string Token(string subject) => Regex.Match(Messages.Last(message =>
            message.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase)).Body, @"Token: ([^\s]+)").Groups[1].Value;
    }

    private sealed class AuthFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly string databaseName = $"auth-{Guid.NewGuid():N}";
        private readonly string environment;
        private readonly TimeSpan? tokenLifetime;
        private readonly bool rejectPlatformAudits;
        private SqliteConnection? connection;
        public RecordingEmails Emails { get; } = new();

        private AuthFactory(string environment, TimeSpan? tokenLifetime, bool rejectPlatformAudits)
        {
            this.environment = environment;
            this.tokenLifetime = tokenLifetime;
            this.rejectPlatformAudits = rejectPlatformAudits;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment).UseTestDatabase()
                .UseSetting("ConnectionStrings:Demeure", $"Data Source={databaseName};Mode=Memory;Cache=Shared;Foreign Keys=True");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountEmailSender>();
                services.AddSingleton<IAccountEmailSender>(Emails);
                services.AddSingleton<IStartupFilter, AuthorizationEndpoints>();
                if (tokenLifetime.HasValue)
                    services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = tokenLifetime.Value);
                if (rejectPlatformAudits)
                    services.AddDbContext<SqliteDemeureDbContext>(options => options.AddInterceptors(new RejectPlatformAudits()));
            });
        }

        public static async Task<AuthFactory> CreateAsync(string environment = "Development", TimeSpan? tokenLifetime = null,
            bool rejectPlatformAudits = false)
        {
            var factory = new AuthFactory(environment, tokenLifetime, rejectPlatformAudits);
            factory.connection = new SqliteConnection($"Data Source={factory.databaseName};Mode=Memory;Cache=Shared;Foreign Keys=True");
            await factory.connection.OpenAsync();
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DemeureDbContext>().Database.MigrateAsync();
            return factory;
        }

        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, HandleCookies = true,
            BaseAddress = new Uri(environment == "Production" ? "https://localhost" : "http://localhost")
        });

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (connection is not null) await connection.DisposeAsync();
        }
    }

    private sealed class AuthorizationEndpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/__tests/owner", (IOrganizationContext context) =>
                    TypedResults.Ok(context.OrganizationId)).RequireAuthorization(ApplicationRoles.OrganizationOwner);
                endpoints.MapGet("/__tests/admin", () => TypedResults.Ok())
                    .RequireAuthorization(ApplicationRoles.SuperAdministrator);
                foreach (var role in new[] { ApplicationRoles.PropertyManager, ApplicationRoles.Tenant, ApplicationRoles.Contractor })
                    endpoints.MapGet($"/__tests/role/{role}", () => TypedResults.Ok()).RequireAuthorization(role);
            });
        };
    }
}
