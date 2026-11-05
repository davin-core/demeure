using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Demeure.Infrastructure.Persistence;

public sealed class PostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<DemeureDbContext>
{
    public DemeureDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DEMEURE_POSTGRES_CONNECTION")
            ?? "Host=localhost;Database=demeure;Username=demeure";
        return new DemeureDbContext(
            new DbContextOptionsBuilder<DemeureDbContext>().UseNpgsql(connection).Options,
            new UnavailableOrganizationContext());
    }
}
