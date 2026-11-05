namespace Demeure.Api.Endpoints;

internal static class DocumentationEndpoints
{
    internal static IEndpointRouteBuilder MapApiDocumentation(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/swagger", (CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return TypedResults.Redirect("/swagger/index.html");
        }).ExcludeFromDescription();
        endpoints.MapGet("/swagger/index.html", (HttpContext context, CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.ContentSecurityPolicy =
                "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'";
            return TypedResults.Content("""
                <!doctype html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width,initial-scale=1">
                  <title>Demeure API v1</title>
                  <link rel="stylesheet" href="/swagger/swagger-ui.css">
                </head>
                <body>
                  <div id="swagger-ui"></div>
                  <script src="/swagger/swagger-ui-bundle.js"></script>
                  <script src="/swagger/swagger-init.js"></script>
                </body>
                </html>
                """, "text/html");
        }).ExcludeFromDescription();
        foreach (var asset in new[] { "swagger-ui.css", "swagger-ui-bundle.js", "swagger-init.js" })
        {
            var contentType = asset.EndsWith(".css", StringComparison.Ordinal) ? "text/css" : "text/javascript";
            endpoints.MapGet($"/swagger/{asset}", (CancellationToken cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return TypedResults.PhysicalFile(Path.Combine(AppContext.BaseDirectory, "SwaggerAssets", asset), contentType);
            }).ExcludeFromDescription();
        }
        return endpoints;
    }
}
