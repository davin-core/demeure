using System.Diagnostics;

namespace Demeure.Api.Middleware;

internal sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        // Exception handling can clear endpoint metadata before this middleware resumes.
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched)";
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = RequestCorrelation.GetTraceId(context)
        });

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "HTTP {Method} {Route} completed with {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                route,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
