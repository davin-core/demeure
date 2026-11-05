using Demeure.Application.Common;
using Demeure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Demeure.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string provider,
        string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Demeure must be configured.");

        services.TryAddScoped<IOrganizationContext, UnavailableOrganizationContext>();
        if (provider == "PostgreSQL")
        {
            services.AddDbContext<DemeureDbContext>(options => options.UseNpgsql(connectionString));
        }
        else if (provider == "Sqlite")
        {
            services.AddDbContext<SqliteDemeureDbContext>(options => options.UseSqlite(connectionString));
            services.AddScoped<DemeureDbContext>(provider => provider.GetRequiredService<SqliteDemeureDbContext>());
        }
        else
        {
            throw new InvalidOperationException("Database:Provider must be PostgreSQL or Sqlite.");
        }

        return services;
    }
}
