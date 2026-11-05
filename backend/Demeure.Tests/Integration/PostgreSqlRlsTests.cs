using Demeure.Application.Common;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Domain.Modules.Properties.Entities;
using Demeure.Domain.Modules.Users.Entities;
using Demeure.Infrastructure.Authentication;
using Demeure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Demeure.Tests.Integration;

public sealed class PostgreSqlRlsTests
{
    [PostgreSqlFact]
    public async Task RlsDeniesMissingForgedAndExpiredContextEvenForTheTableOwner()
    {
        var configured = Environment.GetEnvironmentVariable("DEMEURE_TEST_POSTGRES_ADMIN")!;
        var databaseName = $"demeure_rls_{Guid.NewGuid():N}";
        var roleName = $"demeure_rls_role_{Guid.NewGuid():N}";
        var adminSettings = new NpgsqlConnectionStringBuilder(configured) { Pooling = false };
        await using var admin = new NpgsqlConnection(adminSettings.ConnectionString);
        await admin.OpenAsync();
        var createdDatabase = false;
        var createdRole = false;
        try
        {
            await ExecuteAsync(admin, $"CREATE DATABASE {databaseName}");
            createdDatabase = true;
            await ExecuteAsync(admin, $"CREATE ROLE {roleName} NOLOGIN NOSUPERUSER NOBYPASSRLS");
            createdRole = true;
            var settings = new NpgsqlConnectionStringBuilder(adminSettings.ConnectionString) { Database = databaseName };
            var options = new DbContextOptionsBuilder<DemeureDbContext>().UseNpgsql(settings.ConnectionString).Options;
            var first = new Organization("First", "first");
            var second = new Organization("Second", "second");
            var user = new User("test", Guid.NewGuid().ToString(), "member@example.test", "Member");
            var property = new Property(first.Id, "First property", "1 Street", "City", "12345", "CA");
            var otherProperty = new Property(second.Id, "Second property", "2 Street", "City", "12345", "CA");
            await using (var db = new DemeureDbContext(options, new RlsContext(first.Id)))
            {
                await db.Database.MigrateAsync();
                db.AddRange(first, user, property, new OrganizationMembership(first.Id, user.Id),
                    new IdentityAccount { Id = user.Id, EmailConfirmed = true, Email = user.Email, UserName = user.Email });
                await db.SaveChangesAsync();
            }
            await using (var db = new DemeureDbContext(options, new RlsContext(second.Id)))
            {
                db.AddRange(second, otherProperty);
                await db.SaveChangesAsync();
            }
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"GRANT USAGE ON SCHEMA public TO {roleName}");
            await ExecuteAsync(connection, $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {roleName}");
            await ExecuteAsync(connection, $"ALTER TABLE public.properties OWNER TO {roleName}");
            await ExecuteAsync(connection, $"SET ROLE {roleName}");
            Assert.Equal(0L, await CountAsync(connection));
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await SetContextAsync(connection, first.Id, user.Id, false);
                Assert.Equal(1L, await CountAsync(connection));
                await using var alteredIdentifier = new NpgsqlCommand(
                    "SELECT count(*) FROM public.properties WHERE \"Id\" = @id", connection);
                alteredIdentifier.Parameters.AddWithValue("id", otherProperty.Id);
                Assert.Equal(0L, await alteredIdentifier.ExecuteScalarAsync());
                await transaction.CommitAsync();
            }
            Assert.Equal(0L, await CountAsync(connection));
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await SetContextAsync(connection, second.Id, user.Id, false);
                Assert.Equal(0L, await CountAsync(connection));
                await SetContextAsync(connection, second.Id, user.Id, true);
                Assert.Equal(0L, await CountAsync(connection));
                var forbidden = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection,
                    $"""
                    INSERT INTO public.properties ("Id", "OrganizationId", "Name", "AddressLine1", "City",
                        "PostalCode", "CountryCode", "Status", "CreatedAt", "UpdatedAt")
                    VALUES ('{Guid.NewGuid()}', '{second.Id}', 'Forbidden', 'Street', 'City', '12345', 'CA',
                        'Active', now(), now())
                    """));
                Assert.Equal("42501", forbidden.SqlState);
            }
            await using (var db = new DemeureDbContext(options, new RlsContext(first.Id)))
            {
                var role = new IdentityRole<Guid>("SuperAdministrator")
                {
                    Id = Guid.NewGuid(), NormalizedName = "SUPERADMINISTRATOR"
                };
                db.AddRange(role, new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
                await db.SaveChangesAsync();
            }
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await SetContextAsync(connection, second.Id, user.Id, false);
                Assert.Equal(0L, await CountAsync(connection));
                await SetContextAsync(connection, second.Id, user.Id, true);
                Assert.Equal(1L, await CountAsync(connection));
                await transaction.RollbackAsync();
            }
            Assert.Equal(0L, await CountAsync(connection));
        }
        finally
        {
            if (createdDatabase) await ExecuteAsync(admin, $"DROP DATABASE {databaseName} WITH (FORCE)");
            if (createdRole) await ExecuteAsync(admin, $"DROP ROLE {roleName}");
        }
    }

    private static async Task SetContextAsync(NpgsqlConnection connection, Guid organization, Guid user, bool platform)
    {
        await using var command = new NpgsqlCommand("""
            SELECT set_config('demeure.organization_id', @organization, true),
                   set_config('demeure.user_id', @user, true),
                   set_config('demeure.platform_access', @platform, true)
            """, connection);
        command.Parameters.AddWithValue("organization", organization.ToString());
        command.Parameters.AddWithValue("user", user.ToString());
        command.Parameters.AddWithValue("platform", platform ? "true" : "false");
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT count(*) FROM public.properties", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record RlsContext(Guid? OrganizationId) : IOrganizationContext;

    private sealed class PostgreSqlFactAttribute : FactAttribute
    {
        public PostgreSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEMEURE_TEST_POSTGRES_ADMIN")))
                Skip = "Set DEMEURE_TEST_POSTGRES_ADMIN to an isolated PostgreSQL test server admin connection.";
        }
    }
}
