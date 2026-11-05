using Microsoft.AspNetCore.Antiforgery;

namespace Demeure.Api.Middleware;

internal sealed class CsrfMiddleware(RequestDelegate next, IAntiforgery antiforgery, IProblemDetailsService problems)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api") &&
            !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
            !HttpMethods.IsOptions(context.Request.Method))
        {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = 400, Title = "Invalid CSRF token",
                        Detail = "Request a fresh token from /api/v1/auth/csrf and send it in X-CSRF-TOKEN."
                    }
                });
                return;
            }
        }
        await next(context);
    }
}
