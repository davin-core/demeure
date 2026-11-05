using Demeure.Application.Common.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.RateLimiting;

namespace Demeure.Api.Extensions;

internal static class OpenApiConventions
{
    internal static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                document.Info = new OpenApiInfo
                {
                    Title = "Demeure API",
                    Version = "1.0",
                    Description = "Canonical business routes use /api/v1; /api aliases remain compatible. " +
                        "Successes are DTOs; errors use Problem Details. Login establishes an HttpOnly cookie. " +
                        "Unsafe API requests require a fresh CSRF token. Health routes are unversioned."
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["SessionCookie"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Cookie, Name = "Demeure.Session",
                    Description = "HttpOnly session established by login. Browsers send it automatically; do not paste it into Swagger."
                };
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var document = context.Document ?? throw new InvalidOperationException("OpenAPI document context is required.");
                var authorization = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().ToArray();
                var api = context.Description.RelativePath?.StartsWith("api/v1/", StringComparison.Ordinal) == true;
                operation.Responses ??= new OpenApiResponses();
                if (api)
                {
                    operation.Responses.TryAdd("400", Problem("Invalid request", document));
                    operation.Responses.TryAdd("500", Problem("Unexpected server error", document));
                }
                if (authorization.Length > 0)
                {
                    operation.Responses.TryAdd("401", Problem("Authentication required", document));
                    operation.Responses.TryAdd("403", Problem("Access denied", document));
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("SessionCookie", document)] = []
                    });
                }
                if (context.Description.ActionDescriptor.EndpointMetadata.OfType<EnableRateLimitingAttribute>().Any())
                    operation.Responses.TryAdd("429", Problem("Rate limit exceeded; see Retry-After", document));
                operation.Parameters ??= [];
                if (authorization.Any(value => value.Policy == AuthorizationPolicies.OrganizationResource))
                    operation.Parameters.Add(Header("X-Organization-Id",
                        "Authorized organization selector. Required when multiple memberships exist; never grants membership."));
                if (context.Description.HttpMethod is not ("GET" or "HEAD" or "OPTIONS") &&
                    context.Description.RelativePath?.StartsWith("api/", StringComparison.Ordinal) == true)
                    operation.Parameters.Add(Header("X-CSRF-TOKEN",
                        "Request token from GET /api/v1/auth/csrf with the paired cookie. Swagger obtains it automatically."));
                return Task.CompletedTask;
            });
        });
        return services;
    }

    private static OpenApiParameter Header(string name, string description) => new()
    {
        Name = name, In = ParameterLocation.Header, Required = name == "X-CSRF-TOKEN", Description = description,
        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
    };

    private static OpenApiResponse Problem(string description, OpenApiDocument document) => new()
    {
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/problem+json"] = new()
            {
                Schema = new OpenApiSchemaReference("ProblemDetails", document)
            }
        }
    };
}
