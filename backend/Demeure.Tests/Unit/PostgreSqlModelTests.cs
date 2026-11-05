using Demeure.Application.Common;
using Demeure.Domain.Common;
using Demeure.Domain.Modules.Leases.Entities;
using Demeure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Demeure.Tests.Unit;

public sealed class PostgreSqlModelTests
{
    [Fact]
    public void OrganizationRlsMigrationProtectsEveryBusinessTableAndRequiresLiveMembership()
    {
        using var db = CreateContext();
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        var businessTables = db.Model.GetEntityTypes()
            .Where(entity => typeof(OrganizationEntity).IsAssignableFrom(entity.ClrType) &&
                entity.GetTableName() != "organization_memberships")
            .Select(entity => entity.GetTableName()).ToArray();
        Assert.NotEmpty(businessTables);
        foreach (var table in businessTables)
        {
            Assert.Contains($"ALTER TABLE public.{table} ENABLE ROW LEVEL SECURITY", sql);
            Assert.Contains($"ALTER TABLE public.{table} FORCE ROW LEVEL SECURITY", sql);
            Assert.Contains($"CREATE POLICY demeure_organization_isolation ON public.{table}", sql);
        }
        Assert.Contains("WITH CHECK (public.demeure_can_access_organization", sql);
        Assert.Contains("current_setting('demeure.organization_id', true)", sql);
        Assert.Contains("current_setting('demeure.user_id', true)", sql);
        Assert.Contains("m.\"Status\" = 'Active' AND o.\"Status\" = 'Active'", sql);
        Assert.Contains("r.\"NormalizedName\" = 'SUPERADMINISTRATOR'", sql);
        Assert.Contains("SECURITY INVOKER", sql);
        Assert.DoesNotContain("SECURITY DEFINER", sql);
        var down = db.GetService<IMigrator>().GenerateScript(
            fromMigration: "20261105184128_AddResourceAuthorization",
            toMigration: "20261105182601_AddIdentityAuthentication");
        Assert.Contains("DROP POLICY demeure_organization_isolation", down);
        Assert.Contains("DROP FUNCTION public.demeure_can_access_organization(uuid)", down);
    }

    [Fact]
    public void PostgreSqlModelUsesNativeMoneyUtcAndOrganizationScopedForeignKeys()
    {
        using var db = CreateContext();
        var model = db.GetService<IDesignTimeModel>().Model;

        Assert.Equal(16, model.GetEntityTypes().Count(entity => typeof(Entity).IsAssignableFrom(entity.ClrType)));
        Assert.False(db.Database.HasPendingModelChanges());
        foreach (var entity in model.GetEntityTypes().Where(entity => typeof(Entity).IsAssignableFrom(entity.ClrType)))
        {
            Assert.All(entity.GetProperties(), property =>
            {
                Assert.False(property.IsShadowProperty());
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type == typeof(decimal))
                {
                    Assert.Equal("numeric(18,2)", property.GetColumnType());
                    Assert.Equal(18, property.GetPrecision());
                    Assert.Equal(2, property.GetScale());
                }
                if (type == typeof(DateTimeOffset)) Assert.Equal("timestamp with time zone", property.GetColumnType());
                if (type == typeof(DateOnly)) Assert.Equal("date", property.GetColumnType());
            });
            if (!typeof(OrganizationEntity).IsAssignableFrom(entity.ClrType)) continue;
            Assert.NotEmpty(entity.GetDeclaredQueryFilters());
            foreach (var foreignKey in entity.GetForeignKeys()
                         .Where(key => typeof(OrganizationEntity).IsAssignableFrom(key.PrincipalEntityType.ClrType)))
            {
                Assert.Contains(foreignKey.Properties, property => property.Name == nameof(OrganizationEntity.OrganizationId));
                Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
            }
        }
        Assert.NotNull(model.FindEntityType(typeof(Lease))!.FindProperty(nameof(Lease.TenantId)));
    }

    [Fact]
    public void InitialPostgreSqlMigrationGeneratesIdempotentSqlWithoutConnecting()
    {
        using var db = CreateContext();
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("CREATE TABLE organizations", sql);
        Assert.Contains("CREATE TABLE audit_logs", sql);
        Assert.Contains("numeric(18,2)", sql);
        Assert.Contains("timestamp with time zone", sql);
        Assert.Contains("FOREIGN KEY (\"OrganizationId\", \"PropertyId\")", sql);
        Assert.Contains("ON DELETE RESTRICT", sql);
        Assert.Contains("IF NOT EXISTS", sql);
        Assert.DoesNotContain("AUTOINCREMENT", sql);
    }

    private static DemeureDbContext CreateContext() => new(
        new DbContextOptionsBuilder<DemeureDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test").Options,
        new TestOrganizationContext());

    private sealed class TestOrganizationContext : IOrganizationContext
    {
        public Guid? OrganizationId => Guid.NewGuid();
    }
}
