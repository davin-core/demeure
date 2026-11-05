using Demeure.Api.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Demeure.Api.Endpoints;

/// <summary>Maps operational endpoints independently of business modules.</summary>
public static class HealthEndpoints
{
    /// <summary>Maps readiness and process liveness checks.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/health", (HealthCheckService service, HttpContext context, CancellationToken cancellationToken) =>
            CheckHealthAsync(service, context, includeDependencies: true, cancellationToken))
            .WithName("GetHealth")
            .WithSummary("Check API readiness")
            .WithDescription("Runs all registered health checks. No external dependencies are configured yet.")
            .Produces<HealthResponse>()
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet("/api/health/live", (HealthCheckService service, HttpContext context, CancellationToken cancellationToken) =>
            CheckHealthAsync(service, context, includeDependencies: false, cancellationToken))
            .WithName("GetLiveness")
            .WithSummary("Check API liveness")
            .WithDescription("Checks that the API process can respond without checking external dependencies.")
            .Produces<HealthResponse>();

        return endpoints;
    }

    private static async Task<JsonHttpResult<HealthResponse>> CheckHealthAsync(
        HealthCheckService service,
        HttpContext context,
        bool includeDependencies,
        CancellationToken cancellationToken)
    {
        var report = await service.CheckHealthAsync(
            _ => includeDependencies,
            cancellationToken);
        var status = report.Status switch
        {
            HealthStatus.Healthy => "ok",
            HealthStatus.Degraded => "degraded",
            _ => "unhealthy"
        };

        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Json(
            new HealthResponse(status, "demeure-api"),
            statusCode: report.Status == HealthStatus.Unhealthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK);
    }
}
