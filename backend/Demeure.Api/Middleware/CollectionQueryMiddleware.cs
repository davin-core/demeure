using Demeure.Application.Common.Exceptions;

namespace Demeure.Api.Middleware;

internal sealed record CollectionEndpointMetadata;

internal sealed class CollectionQueryMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> Allowed =
        new(["page", "pageSize", "status", "sortBy", "sortDirection"], StringComparer.OrdinalIgnoreCase);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<CollectionEndpointMetadata>() is not null)
        {
            var errors = context.Request.Query.Where(value => !Allowed.Contains(value.Key) || value.Value.Count != 1)
                .ToDictionary(value => value.Key, value => new[]
                    { Allowed.Contains(value.Key) ? "Supply a single query value." : "Unsupported query parameter." });
            if (errors.Count > 0) throw new RequestValidationException(errors);
        }
        await next(context);
    }
}
