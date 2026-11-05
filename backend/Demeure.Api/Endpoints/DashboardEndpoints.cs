using System.Globalization;
using Demeure.Api.Extensions;
using Demeure.Application.Common.Authorization;
using Demeure.Application.Common.Exceptions;
using Demeure.Application.Modules.Reporting.Contracts;

namespace Demeure.Api.Endpoints;

/// <summary>Organization-scoped landlord reporting endpoints.</summary>
public static class DashboardEndpoints
{
    /// <summary>Maps the landlord dashboard summary without introducing business writes.</summary>
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/dashboard", async (HttpContext http, IDashboardReader reader, CancellationToken cancellationToken) =>
        {
            var query = http.Request.Query;
            if (query.Any(value => value.Value.Count != 1 || value.Key is not ("startDate" or "endDate" or "propertyId")))
                throw Invalid();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var start = new DateOnly(today.Year, today.Month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            if (query.TryGetValue("startDate", out var from) && !DateOnly.TryParseExact(from.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start)) throw Invalid();
            if (query.TryGetValue("endDate", out var to) && !DateOnly.TryParseExact(to.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end)) throw Invalid();
            Guid? property = null;
            if (query.TryGetValue("propertyId", out var id))
            {
                if (!Guid.TryParse(id.ToString(), out var parsed) || parsed == Guid.Empty) throw Invalid();
                property = parsed;
            }
            return TypedResults.Ok(await reader.ReadAsync(new(start, end, property), cancellationToken));
        }).RequireAuthorization(AuthorizationPolicies.OrganizationResource)
            .WithName("GetLandlordDashboard").WithTags("Reporting")
            .WithSummary("Get live landlord dashboard metrics for an authorized organization and optional property")
            .WithDescription("Inclusive UTC dates default to the current month; maximum five calendar years. Inventory and pending maintenance are current. " +
                "Financial totals remain separate by currency. A foreign, archived or unassigned property returns 404.")
            .WithResourceConventions().ProducesValidationProblem().ProducesProblem(404);
        return endpoints;
    }

    private static RequestValidationException Invalid() => new(new Dictionary<string, string[]>
    {
        ["filters"] = ["Supply each supported query once: startDate/endDate as yyyy-MM-dd and an optional valid propertyId."]
    });
}
