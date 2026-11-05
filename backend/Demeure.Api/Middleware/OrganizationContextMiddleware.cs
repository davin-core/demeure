using Demeure.Application.Modules.Users.Contracts;
using Demeure.Application.Common;

namespace Demeure.Api.Middleware;

internal sealed class OrganizationContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuthenticationService service, IProblemDetailsService problems,
        IOrganizationContext organizationContext, ILogger<OrganizationContextMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            Guid? organizationId = null;
            if (context.Request.Headers.TryGetValue("X-Organization-Id", out var supplied))
            {
                if (supplied.Count != 1 || !Guid.TryParse(supplied[0], out var parsed) || parsed == Guid.Empty)
                {
                    await RejectAsync(context, problems, 400, "Invalid organization identifier");
                    return;
                }
                organizationId = parsed;
            }
            else
            {
                var session = await service.GetSessionAsync(context.RequestAborted);
                if (session?.Memberships.Count == 1) organizationId = session.Memberships[0].OrganizationId;
            }

            if (organizationId.HasValue &&
                !await service.SelectOrganizationAsync(organizationId.Value, context.RequestAborted))
            {
                await RejectAsync(context, problems, 403, "Organization access denied");
                return;
            }
        }
        using var scope = logger.BeginScope(
            new Dictionary<string, object?> { ["OrganizationId"] = organizationContext.OrganizationId });
        await next(context);
    }

    private static ValueTask RejectAsync(HttpContext context, IProblemDetailsService problems, int status, string title)
    {
        context.Response.StatusCode = status;
        return problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = status, Title = title }
        });
    }
}
