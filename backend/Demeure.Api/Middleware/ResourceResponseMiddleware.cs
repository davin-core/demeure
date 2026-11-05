namespace Demeure.Api.Middleware;

internal sealed record ResourceEndpointMetadata;

internal sealed class ResourceResponseMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<ResourceEndpointMetadata>() is not null)
            context.Response.Headers.CacheControl = "no-store";
        await next(context);
    }
}
