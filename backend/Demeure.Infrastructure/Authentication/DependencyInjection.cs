using Demeure.Application.Common;
using Demeure.Application.Modules.Notifications.Contracts;
using Demeure.Application.Modules.Users.Contracts;
using Demeure.Domain.Modules.Users.Entities;
using Demeure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demeure.Infrastructure.Authentication;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, bool development)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<OrganizationContext>();
        services.AddScoped<IOrganizationContext>(provider => provider.GetRequiredService<OrganizationContext>());
        services.AddScoped<Demeure.Application.Modules.Users.Contracts.IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAccountEmailSender, AccountEmailSender>();
        services.AddScoped<Demeure.Infrastructure.Authorization.OrganizationDataTransaction>();
        services.AddScoped<Demeure.Application.Common.Authorization.IResourceReader,
            Demeure.Infrastructure.Authorization.ResourceReader>();
        services.AddIdentity<IdentityAccount, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 6;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
            .AddEntityFrameworkStores<DemeureDbContext>()
            .AddClaimsPrincipalFactory<IdentityClaimsFactory>()
            .AddDefaultTokenProviders();
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(1));
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Demeure.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async context =>
            {
                var issued = context.Properties.IssuedUtc;
                var expires = context.Properties.ExpiresUtc;
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                if (context.Principal?.Identity?.IsAuthenticated != true) return;
                // Stamp validation refreshes claims, but must not extend our fixed session lifetime.
                context.ShouldRenew = false;
                context.Properties.IssuedUtc = issued;
                context.Properties.ExpiresUtc = expires;
                var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<IdentityAccount>>();
                var account = await manager.GetUserAsync(context.Principal);
                var db = context.HttpContext.RequestServices.GetRequiredService<DemeureDbContext>();
                var active = account is not null && account.EmailConfirmed &&
                    await db.Users.AnyAsync(user => user.Id == account.Id && user.Status == UserStatus.Active,
                        context.HttpContext.RequestAborted);
                if (!active)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });
        return services;
    }
}
