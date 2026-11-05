using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Demeure.Infrastructure.Persistence;

public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteDemeureDbContext>
{
    public SqliteDemeureDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DEMEURE_SQLITE_CONNECTION")
            ?? "Data Source=demeure.db;Foreign Keys=True";
        return new SqliteDemeureDbContext(
            new DbContextOptionsBuilder<SqliteDemeureDbContext>().UseSqlite(connection).Options,
            new UnavailableOrganizationContext());
    }
}
