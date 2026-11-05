using System.Net.Mail;
using System.Threading.RateLimiting;
using Demeure.Application.Modules.Users.Contracts;
using Demeure.Infrastructure.Authentication;
using Demeure.Application.Common.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;

namespace Demeure.Api.Extensions;

internal static class AuthenticationConfiguration
{
    internal static IServiceCollection AddAuthenticationConfiguration(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddIdentityAuthentication(environment.IsDevelopment());
        services.AddDataProtection().SetApplicationName("Demeure");
        services.AddOptions<AccountEmailOptions>().Bind(configuration.GetSection("AccountEmail"))
            .PostConfigure(options =>
            {
                if (environment.IsDevelopment() && options.Delivery == "Pickup" &&
                    string.IsNullOrWhiteSpace(options.PickupDirectory))
                    options.PickupDirectory = Path.Combine(environment.ContentRootPath, "EmailMessages");
            })
            .Validate(options => MailAddress.TryCreate(options.From, out _), "AccountEmail:From must be valid.")
            .Validate(options => options.Delivery == "Smtp"
                ? !string.IsNullOrWhiteSpace(options.Host) && options.Port is > 0 and <= 65535
                : environment.IsDevelopment() && options.Delivery == "Pickup" && Path.IsPathFullyQualified(options.PickupDirectory),
                "Configure SMTP delivery, or an absolute development-only pickup directory.")
            .ValidateOnStart();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "Demeure.Csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.OrganizationResource, policy =>
                policy.RequireAuthenticatedUser().RequireClaim("demeure:organization_id")
                    .RequireRole(ApplicationRoles.OrganizationOwner, ApplicationRoles.PropertyManager,
                        ApplicationRoles.Tenant, ApplicationRoles.Contractor));
            options.AddPolicy(AuthorizationPolicies.PlatformResource, policy =>
                policy.RequireAuthenticatedUser().RequireRole(ApplicationRoles.SuperAdministrator));
            options.AddPolicy(ApplicationRoles.SuperAdministrator, policy =>
                policy.RequireAuthenticatedUser().RequireRole(ApplicationRoles.SuperAdministrator));
            foreach (var role in new[] { ApplicationRoles.OrganizationOwner, ApplicationRoles.PropertyManager,
                         ApplicationRoles.Tenant, ApplicationRoles.Contractor })
                options.AddPolicy(role, policy => policy.RequireAuthenticatedUser().RequireRole(role)
                    .RequireClaim("demeure:organization_id"));
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth-sensitive", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retry.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context.HttpContext,
                        ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                        { Status = 429, Title = "Too many authentication requests" }
                    });
            };
        });
        return services;
    }
}
