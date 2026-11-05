using System.Security.Claims;
using System.Text;
using Demeure.Application.Common.Exceptions;
using Demeure.Application.Modules.Notifications.Contracts;
using Demeure.Application.Modules.Users.Contracts;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Domain.Modules.Users.Entities;
using Demeure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Demeure.Infrastructure.Authentication;

internal sealed class AuthenticationService(
    DemeureDbContext db,
    UserManager<IdentityAccount> users,
    SignInManager<IdentityAccount> signIn,
    IHttpContextAccessor httpContext,
    OrganizationContext organizationContext,
    IAccountEmailSender emailSender) : IAuthenticationService
{
    public async Task<RegistrationResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null ||
            await db.Users.AnyAsync(user => user.NormalizedEmail == email.ToUpperInvariant(), cancellationToken))
            throw Invalid("email", "This email cannot be registered.");
        if (await db.Organizations.IgnoreQueryFilters().AnyAsync(
                organization => organization.Slug == request.OrganizationSlug, cancellationToken))
            throw Invalid("organizationSlug", "This organization slug is unavailable.");

        var organization = new Organization(request.OrganizationName, request.OrganizationSlug);
        var userId = Guid.NewGuid();
        User user;
        try
        {
            user = new User("demeure-local", userId.ToString(), email, request.DisplayName);
        }
        catch (ArgumentException)
        {
            throw Invalid("profile", "A valid email and nonblank display name are required.");
        }
        var account = new IdentityAccount
        {
            Id = user.Id, UserName = user.Email, Email = user.Email, LockoutEnabled = true
        };

        organizationContext.Set(organization.Id);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            db.AddRange(organization, user, new OrganizationMembership(organization.Id, user.Id, MembershipRole.Owner));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                var result = await users.CreateAsync(account, request.Password);
                if (!result.Succeeded) throw Invalid("account", SafeErrors(result));
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                throw Invalid("registration", "The email or organization slug is unavailable.");
            }
        }
        finally
        {
            organizationContext.Set(null);
        }

        await SendConfirmationAsync(account, cancellationToken);
        return new RegistrationResponse(user.Id, organization.Id);
    }

    public async Task<bool> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var account = await users.FindByEmailAsync(request.Email.Trim());
        if (account is null) return false;
        var profile = await db.Users.SingleOrDefaultAsync(user => user.Id == account.Id, cancellationToken);
        if (profile?.Status != UserStatus.Active) return false;
        var result = await signIn.PasswordSignInAsync(account, request.Password, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded;
    }

    public async Task LogoutAsync(bool allSessions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (allSessions)
        {
            var account = await users.GetUserAsync(Principal);
            if (account is not null)
            {
                var result = await users.UpdateSecurityStampAsync(account);
                if (!result.Succeeded) throw new InvalidOperationException("Session revocation failed.");
            }
        }
        await signIn.SignOutAsync();
    }

    public async Task<SessionResponse?> GetSessionAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user?.Status != UserStatus.Active) return null;
        var account = await users.FindByIdAsync(userId.ToString());
        if (account is null || !account.EmailConfirmed) return null;
        var memberships = await (
            from membership in db.OrganizationMemberships.IgnoreQueryFilters().AsNoTracking()
            join organization in db.Organizations.IgnoreQueryFilters().AsNoTracking()
                on membership.OrganizationId equals organization.Id
            where membership.UserId == userId && membership.Status == MembershipStatus.Active &&
                  organization.Status == OrganizationStatus.Active
            select new { organization.Id, organization.Name, membership.Role }
        ).ToListAsync(cancellationToken);
        var roles = await users.GetRolesAsync(account);
        return new SessionResponse(user.Id, user.Email, user.DisplayName,
            roles.Where(role => role == ApplicationRoles.SuperAdministrator).ToArray(),
            memberships.Select(value => new MembershipResponse(value.Id, value.Name, Role(value.Role))).ToArray());
    }

    public async Task<bool> SelectOrganizationAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        organizationContext.Set(null);
        var session = await GetSessionAsync(cancellationToken);
        var membership = session?.Memberships.SingleOrDefault(value => value.OrganizationId == organizationId);
        if (membership is null) return false;
        organizationContext.Set(organizationId);
        if (Principal.Identity is ClaimsIdentity scopedIdentity)
            scopedIdentity.AddClaim(new Claim("demeure:organization_id", organizationId.ToString()));
        if (Principal.Identity is ClaimsIdentity identity && membership.Role.Length > 0)
            identity.AddClaim(new Claim(identity.RoleClaimType, membership.Role));
        return true;
    }

    public async Task SendVerificationAsync(string email, CancellationToken cancellationToken)
    {
        var account = await users.FindByEmailAsync(email.Trim());
        if (account is null || account.EmailConfirmed) return;
        await SendConfirmationAsync(account, cancellationToken);
    }

    public async Task<bool> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = Decode(request.Token);
        if (token is null) return false;
        var account = await users.FindByIdAsync(request.UserId.ToString());
        if (account is null || account.EmailConfirmed) return false;
        return (await users.ConfirmEmailAsync(account, token)).Succeeded;
    }

    public async Task SendPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var account = await users.FindByEmailAsync(email.Trim());
        if (account is null || !account.EmailConfirmed) return;
        var token = Encode(await users.GeneratePasswordResetTokenAsync(account));
        await emailSender.SendAsync(account.Email!, "Demeure password reset",
            $"A password reset was requested for your Demeure account.\n\nToken: {token}\n\n" +
            "Submit your email, this token, and your new password to POST /api/v1/auth/reset-password.\n" +
            "This token expires in one hour. If you did not request this, ignore this message.",
            cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = Decode(request.Token);
        if (token is null) return false;
        var account = await users.FindByEmailAsync(request.Email.Trim());
        if (account is null || !account.EmailConfirmed) return false;
        var result = await users.ResetPasswordAsync(account, token, request.NewPassword);
        if (!result.Succeeded) return false;
        var unlock = await users.SetLockoutEndDateAsync(account, null);
        var reset = await users.ResetAccessFailedCountAsync(account);
        if (!unlock.Succeeded || !reset.Succeeded) throw new InvalidOperationException("Account lockout reset failed.");
        return true;
    }

    private async Task SendConfirmationAsync(IdentityAccount account, CancellationToken cancellationToken)
    {
        var token = Encode(await users.GenerateEmailConfirmationTokenAsync(account));
        await emailSender.SendAsync(account.Email!, "Verify your Demeure email",
            $"Verify your email before signing in to Demeure.\n\nUserId: {account.Id}\nToken: {token}\n\n" +
            "Submit these values to POST /api/v1/auth/confirm-email.\nThis token expires in one hour.",
            cancellationToken);
    }

    private ClaimsPrincipal Principal => httpContext.HttpContext?.User ?? new ClaimsPrincipal();

    private static string Role(MembershipRole role) => role switch
    {
        MembershipRole.Owner => ApplicationRoles.OrganizationOwner,
        MembershipRole.Administrator or MembershipRole.Manager => ApplicationRoles.PropertyManager,
        MembershipRole.Tenant => ApplicationRoles.Tenant,
        MembershipRole.Contractor => ApplicationRoles.Contractor,
        _ => ""
    };

    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    private static string? Decode(string token)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); }
        catch (FormatException) { return null; }
    }

    private static RequestValidationException Invalid(string field, params string[] errors) =>
        new(new Dictionary<string, string[]> { [field] = errors });

    private static string[] SafeErrors(IdentityResult result) => result.Errors.Select(error => error.Code switch
    {
        "PasswordTooShort" => "Password must contain at least 12 characters.",
        "PasswordRequiresNonAlphanumeric" => "Password must contain a symbol.",
        "PasswordRequiresDigit" => "Password must contain a digit.",
        "PasswordRequiresLower" => "Password must contain a lowercase letter.",
        "PasswordRequiresUpper" => "Password must contain an uppercase letter.",
        "PasswordRequiresUniqueChars" => "Password must contain at least six distinct characters.",
        _ => "The account could not be registered with these credentials."
    }).Distinct().ToArray();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" } or
            Microsoft.Data.Sqlite.SqliteException { SqliteExtendedErrorCode: 2067 or 1555 };
}
