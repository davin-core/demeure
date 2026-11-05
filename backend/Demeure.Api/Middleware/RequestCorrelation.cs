using System.Diagnostics;

namespace Demeure.Api.Middleware;

internal static class RequestCorrelation
{
    internal static string GetTraceId(HttpContext context) =>
        Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
}
