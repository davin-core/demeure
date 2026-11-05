using System.Security.Claims;
using Demeure.Application.Modules.Users.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Demeure.Infrastructure.Authentication;

internal sealed class IdentityClaimsFactory(
    UserManager<IdentityAccount> users,
    RoleManager<IdentityRole<Guid>> roles,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<IdentityAccount, IdentityRole<Guid>>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(IdentityAccount user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        // Organization roles are resolved afresh from authorized membership, never global Identity roles.
        foreach (var claim in identity.FindAll(identity.RoleClaimType)
                     .Where(claim => claim.Value != ApplicationRoles.SuperAdministrator).ToArray())
            identity.RemoveClaim(claim);
        return identity;
    }
}
