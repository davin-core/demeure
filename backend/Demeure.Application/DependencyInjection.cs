using Microsoft.Extensions.DependencyInjection;

namespace Demeure.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register requested modules explicitly here as their use cases are implemented.
        return services;
    }
}
