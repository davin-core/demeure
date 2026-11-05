using Demeure.Api.Endpoints;
using Demeure.Api.Modules.Users;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Demeure.Api.Middleware;

namespace Demeure.Api.Extensions;

internal static class ApiConventions
{
    internal const string CurrentPrefix = "/api/v1";

    internal static IEndpointRouteBuilder MapVersionedApi(this IEndpointRouteBuilder endpoints)
    {
        var current = endpoints.MapGroup(CurrentPrefix);
        current.MapAuthenticationEndpoints();
        current.MapResourceEndpoints();
        current.MapDashboardEndpoints();

        var compatibility = endpoints.MapGroup("/api").ExcludeFromDescription();
        compatibility.MapAuthenticationEndpoints();
        compatibility.MapResourceEndpoints();
        compatibility.MapDashboardEndpoints();
        ((IEndpointConventionBuilder)compatibility).Finally(builder =>
        {
            var name = builder.Metadata.OfType<IEndpointNameMetadata>().LastOrDefault()?.EndpointName;
            if (name is null) return;
            builder.Metadata.Add(new EndpointNameMetadata($"{name}Compatibility"));
            builder.Metadata.Add(new RouteNameMetadata($"{name}Compatibility"));
        });
        return endpoints;
    }

    internal static RouteHandlerBuilder WithResourceConventions(this RouteHandlerBuilder endpoint) =>
        endpoint.ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
            .ProducesProblem(500).WithMetadata(new ResourceEndpointMetadata());

    internal static RouteHandlerBuilder WithCollectionConventions(this RouteHandlerBuilder endpoint) =>
        endpoint.WithResourceConventions().ProducesValidationProblem()
            .WithMetadata(new CollectionEndpointMetadata());
}
