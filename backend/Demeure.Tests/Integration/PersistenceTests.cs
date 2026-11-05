using System.ComponentModel.DataAnnotations;
using Demeure.Application.Common;
using Demeure.Application.Common.Exceptions;
using Demeure.Domain.Common;
using Demeure.Domain.Modules.Applications.Entities;
using Demeure.Domain.Modules.Documents.Entities;
using Demeure.Domain.Modules.Expenses.Entities;
using Demeure.Domain.Modules.Leases.Entities;
using Demeure.Domain.Modules.Maintenance.Entities;
using Demeure.Domain.Modules.Notifications.Entities;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Domain.Modules.Payments.Entities;
using Demeure.Domain.Modules.Properties.Entities;
using Demeure.Domain.Modules.Tenants.Entities;
using RentalUnit = Demeure.Domain.Modules.Units.Entities.Unit;
using Demeure.Domain.Modules.Users.Entities;
using Demeure.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Demeure.Tests.Integration;

public sealed class PersistenceTests
{
    [Fact]
    public async Task InitialSqliteMigrationCreatesOnlyTheRequestedTablesAndRollsBack()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var tables = db.Model.GetEntityTypes().Where(entity => typeof(Entity).IsAssignableFrom(entity.ClrType))
            .Select(entity => entity.GetTableName()).Order().ToArray();

        Assert.Equal(16, tables.Length);
        Assert.Equal(new[] { "20261105181037_InitialSqlite", "20261105182604_AddIdentityAuthentication",
            "20261105184131_AddResourceAuthorization" }, await db.Database.GetAppliedMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.All(db.Model.GetEntityTypes().Where(entity => typeof(Entity).IsAssignableFrom(entity.ClrType)), entity =>
        {
            Assert.Equal(nameof(Entity.Id), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
            Assert.All(entity.GetProperties(), property => Assert.False(property.IsShadowProperty()));
            Assert.All(entity.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        });

        await db.GetService<IMigrator>().MigrateAsync("0");
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        await db.Database.MigrateAsync();
        Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task QueriesAreOrganizationScopedAndMissingContextFailsClosed()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var first = fixture.Context(fixture.OrganizationId);
        await using var second = fixture.Context(fixture.OtherOrganizationId);
        await using var missing = fixture.Context(null);

        Assert.Equal(fixture.PropertyId, Assert.Single(await first.Properties.ToListAsync()).Id);
        Assert.Equal(fixture.OtherPropertyId, Assert.Single(await second.Properties.ToListAsync()).Id);
        Assert.Single(await first.Organizations.ToListAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => missing.Properties.ToListAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => missing.Organizations.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveGuardsRejectCrossOrganizationAndMissingContext(bool missingContext)
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(missingContext ? null : fixture.OrganizationId);
        db.Properties.Add(NewProperty(fixture.OtherOrganizationId));

        await Assert.ThrowsAsync<ForbiddenException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task CrossOrganizationReadsCannotBeReattachedForModification()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var foreignProperty = await db.Properties.IgnoreQueryFilters().SingleAsync(value => value.Id == fixture.OtherPropertyId);
        foreignProperty.Rename("Forbidden change");

        await Assert.ThrowsAsync<ForbiddenException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DatabaseForeignKeysRejectCrossOrganizationReferencesEvenWithRawSql()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);

        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO units ("Id", "OrganizationId", "PropertyId", "Number", "Bedrooms", "Bathrooms", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {fixture.OrganizationId}, {fixture.OtherPropertyId}, {"foreign"}, {1}, {1}, {"Available"}, {DateTimeOffset.UtcNow.UtcTicks}, {DateTimeOffset.UtcNow.UtcTicks})
            """));
    }

    [Fact]
    public async Task UnitNumbersAreUniqueWithinPropertyButNotAcrossProperties()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        db.Units.Add(new RentalUnit(fixture.OrganizationId, fixture.PropertyId, "101"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var property = NewProperty(fixture.OrganizationId);
        db.Add(property);
        db.Add(new RentalUnit(fixture.OrganizationId, property.Id, "101"));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ExpenseAndMaintenanceUnitsMustBelongToTheirSpecifiedProperty()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var property = NewProperty(fixture.OrganizationId);
        db.Add(property);
        await db.SaveChangesAsync();
        db.Expenses.Add(new Expense
        {
            OrganizationId = fixture.OrganizationId, PropertyId = property.Id, UnitId = fixture.UnitId,
            Description = "Repairs", Amount = 10m, Currency = "USD", IncurredDate = new DateOnly(2026, 1, 1)
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.MaintenanceRequests.Add(new MaintenanceRequest
        {
            OrganizationId = fixture.OrganizationId, PropertyId = property.Id, UnitId = fixture.UnitId,
            Title = "Repair", Description = "Fixture failure"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task LeaseApplicationMustMatchItsTenantAndUnit()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var tenant = new Tenant(fixture.OrganizationId, "Other", "Applicant");
        var application = new RentalApplication
        {
            OrganizationId = fixture.OrganizationId, UnitId = fixture.UnitId, TenantId = tenant.Id
        };
        db.AddRange(tenant, application);
        await db.SaveChangesAsync();
        db.Leases.Add(WithApplication(application.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Lease WithApplication(Guid applicationId) => new(fixture.OrganizationId, fixture.UnitId,
            fixture.TenantId, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), 1200m, 0m, "USD", applicationId);
    }

    [Fact]
    public async Task MonetaryValuesRoundTripExactlyAndRejectExtraFractionalDigits()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var lease = NewLease(fixture);
        lease.UpdateTerms(lease.StartDate, lease.EndDate, 1234.56m, 999.99m, lease.Currency);
        db.Add(lease);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.Leases.SingleAsync();
        Assert.Equal(1234.56m, loaded.MonthlyRent);
        Assert.Equal(999.99m, loaded.SecurityDeposit);
        db.Entry(loaded).Property(value => value.MonthlyRent).CurrentValue = 1.001m;
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task UtcInstantsAndCalendarDatesRoundTripWithoutTimezoneAmbiguity()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var lease = NewLease(fixture);
        var instant = new DateTimeOffset(2026, 1, 2, 10, 30, 0, TimeSpan.FromHours(3));
        var payment = new Payment(fixture.OrganizationId, lease.Id, 25.50m, "USD", new DateOnly(2026, 1, 2));
        payment.Complete(instant);
        db.AddRange(lease, payment);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var loaded = await db.Payments.SingleAsync();

        Assert.Equal(TimeSpan.Zero, loaded.PaidAt!.Value.Offset);
        Assert.Equal(instant.ToUniversalTime(), loaded.PaidAt);
        Assert.Equal(TimeSpan.Zero, loaded.CreatedAt.Offset);
        Assert.Equal(new DateOnly(2026, 1, 2), loaded.DueDate);
    }

    [Fact]
    public async Task InvalidDatesAndAmountsAreRejectedByDatabaseConstraints()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var lease = NewLease(fixture);
        db.Add(lease);
        db.Entry(lease).Property(value => value.EndDate).CurrentValue = lease.StartDate;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        lease = NewLease(fixture);
        db.Add(lease);
        db.Entry(lease).Property(value => value.MonthlyRent).CurrentValue = -1m;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task MemberRelationshipsRejectUsersWithoutOrganizationMembership()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var outsider = new User("test", Guid.NewGuid().ToString(), "outsider@example.test", "Outside user");
        db.Add(outsider);
        await db.SaveChangesAsync();
        db.Notifications.Add(new Notification
        {
            OrganizationId = fixture.OrganizationId, RecipientUserId = outsider.Id, Title = "Test", Body = "Test"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task AuditLogsAreAppendOnlyAndReferencedResourcesCannotCascadeDelete()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var audit = new AuditLog
        {
            OrganizationId = fixture.OrganizationId, ActorUserId = fixture.UserId,
            Action = "Created", ResourceType = "Property", ResourceId = fixture.PropertyId
        };
        db.Add(audit);
        await db.SaveChangesAsync();
        db.Remove(audit);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Remove(await db.Properties.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DocumentsAllowAtMostOneParentAndDoNotStoreFileContents()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var lease = NewLease(fixture);
        db.Add(lease);
        await db.SaveChangesAsync();
        db.Documents.Add(new Document
        {
            OrganizationId = fixture.OrganizationId, PropertyId = fixture.PropertyId, LeaseId = lease.Id,
            UploadedByUserId = fixture.UserId, FileName = "lease.pdf", ContentType = "application/pdf",
            StorageKey = "test/lease.pdf", SizeBytes = 100
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task InvalidStatusesAreRejectedByBothTrackedValidationAndDatabaseConstraints()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var property = await db.Properties.SingleAsync();
        db.Entry(property).Property(value => value.Status).CurrentValue = (PropertyStatus)999;
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE properties SET \"Status\" = {"NotAStatus"} WHERE \"Id\" = {fixture.PropertyId}"));
    }

    [Fact]
    public async Task UpdatesPreserveCreationTimeAndAdvanceUpdateTime()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var property = await db.Properties.SingleAsync();
        var creationTime = property.CreatedAt;
        property.CreatedAt = creationTime.AddYears(-1);
        property.UpdatedAt = creationTime.AddYears(-1);
        property.Rename("Updated property");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var loaded = await db.Properties.SingleAsync();

        Assert.Equal(creationTime, loaded.CreatedAt);
        Assert.True(loaded.UpdatedAt >= creationTime);
    }

    [Fact]
    public async Task MoneyPrecisionLimitAndRequiredStringLengthsAreEnforced()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var lease = NewLease(fixture);
        db.Add(lease);
        db.Entry(lease).Property(value => value.MonthlyRent).CurrentValue = 10000000000000000m;
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var property = NewProperty(fixture.OrganizationId);
        db.Add(property);
        db.Entry(property).Property(value => value.Name).CurrentValue = new string('x', 201);
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
        db.Entry(property).Property(value => value.Name).CurrentValue = " ";
        await Assert.ThrowsAsync<ValidationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EntityLifecycleMethodsPersistAndContinueAfterMaterialization()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var organization = await db.Organizations.SingleAsync();
        var user = await db.Users.SingleAsync();
        var membership = await db.OrganizationMemberships.SingleAsync();
        var property = await db.Properties.SingleAsync();
        var unit = await db.Units.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        organization.ChangeStatus(OrganizationStatus.Suspended);
        user.UpdateProfile("updated@example.test", "Updated user");
        user.ChangeStatus(UserStatus.Disabled);
        membership.ChangeRole(MembershipRole.Manager);
        membership.ChangeStatus(MembershipStatus.Suspended);
        property.ChangeStatus(PropertyStatus.Inactive);
        unit.ChangeStatus(Demeure.Domain.Modules.Units.Entities.UnitStatus.Occupied);
        tenant.ChangeStatus(TenantStatus.Inactive);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        organization = await db.Organizations.SingleAsync();
        user = await db.Users.SingleAsync();
        membership = await db.OrganizationMemberships.SingleAsync();
        property = await db.Properties.SingleAsync();
        unit = await db.Units.SingleAsync();
        tenant = await db.Tenants.SingleAsync();
        Assert.Equal(OrganizationStatus.Suspended, organization.Status);
        Assert.Equal(UserStatus.Disabled, user.Status);
        Assert.Equal("UPDATED@EXAMPLE.TEST", user.NormalizedEmail);
        Assert.Equal(MembershipRole.Manager, membership.Role);
        Assert.Equal(MembershipStatus.Suspended, membership.Status);
        Assert.Equal(PropertyStatus.Inactive, property.Status);
        Assert.Equal(Demeure.Domain.Modules.Units.Entities.UnitStatus.Occupied, unit.Status);
        Assert.Equal(TenantStatus.Inactive, tenant.Status);

        organization.ChangeStatus(OrganizationStatus.Active);
        user.ChangeStatus(UserStatus.Active);
        membership.ChangeStatus(MembershipStatus.Active);
        property.ChangeStatus(PropertyStatus.Active);
        unit.ChangeStatus(Demeure.Domain.Modules.Units.Entities.UnitStatus.Available);
        tenant.ChangeStatus(TenantStatus.Active);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(PropertyStatus.Active, (await db.Properties.SingleAsync()).Status);
        Assert.Equal(OrganizationStatus.Active, (await db.Organizations.SingleAsync()).Status);
    }

    [Fact]
    public async Task TerminalLeaseAndPaymentStatesRemainProtectedAfterReload()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        var lease = NewLease(fixture);
        lease.Activate();
        var paidAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var payment = new Payment(fixture.OrganizationId, lease.Id, 10m, "USD", lease.StartDate);
        payment.Complete(paidAt, "receipt");
        db.AddRange(lease, payment);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        lease = await db.Leases.SingleAsync();
        payment = await db.Payments.SingleAsync();
        Assert.Equal(LeaseStatus.Active, lease.Status);
        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.Throws<InvalidOperationException>(() => lease.UpdateTerms(lease.StartDate, lease.EndDate, 1500m, 0m, "USD"));
        lease.End(lease.EndDate);
        payment.Refund();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        lease = await db.Leases.SingleAsync();
        payment = await db.Payments.SingleAsync();
        Assert.Equal(LeaseStatus.Ended, lease.Status);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(paidAt, payment.PaidAt);
        Assert.Equal("receipt", payment.Reference);
        Assert.Throws<InvalidOperationException>(() => lease.Activate());
        Assert.Throws<InvalidOperationException>(() => payment.Retry());
    }

    [Fact]
    public async Task GlobalUserEmailsAreCaseInsensitiveUniqueAndIdentitiesAreUnique()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        await using var db = fixture.Context(fixture.OrganizationId);
        db.Users.Add(new User("test", "different", " USER@EXAMPLE.TEST ", "Duplicate email"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Users.Add(new User("test", "user", "another@example.test", "Duplicate identity"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static Property NewProperty(Guid organizationId) =>
        new(organizationId, "Test property", "123 Test Street", "Test City", "12345", "US");

    private static Lease NewLease(DatabaseFixture fixture) => new(fixture.OrganizationId, fixture.UnitId,
        fixture.TenantId, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), 1200m, 0m, "USD");

    private sealed class TestOrganizationContext(Guid? id) : IOrganizationContext
    {
        public Guid? OrganizationId => id;
    }

    private sealed class DatabaseFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
        public Guid OrganizationId { get; } = Guid.NewGuid();
        public Guid OtherOrganizationId { get; } = Guid.NewGuid();
        public Guid PropertyId { get; private set; }
        public Guid OtherPropertyId { get; private set; }
        public Guid UnitId { get; private set; }
        public Guid TenantId { get; private set; }
        public Guid UserId { get; private set; }

        public SqliteDemeureDbContext Context(Guid? organizationId) => new(
            new DbContextOptionsBuilder<SqliteDemeureDbContext>().UseSqlite(connection).Options,
            new TestOrganizationContext(organizationId));

        public static async Task<DatabaseFixture> CreateAsync()
        {
            var fixture = new DatabaseFixture();
            await fixture.connection.OpenAsync();
            await using var db = fixture.Context(fixture.OrganizationId);
            await db.Database.MigrateAsync();
            var user = new User("test", "user", "user@example.test", "Test user");
            var property = NewProperty(fixture.OrganizationId);
            var unit = new RentalUnit(fixture.OrganizationId, property.Id, "101");
            var tenant = new Tenant(fixture.OrganizationId, "Test", "Tenant", user.Id);
            db.AddRange(
                new Organization("First", "first", fixture.OrganizationId), user,
                new OrganizationMembership(fixture.OrganizationId, user.Id),
                property, unit, tenant);
            await db.SaveChangesAsync();
            fixture.PropertyId = property.Id;
            fixture.UnitId = unit.Id;
            fixture.TenantId = tenant.Id;
            fixture.UserId = user.Id;
            await using var other = fixture.Context(fixture.OtherOrganizationId);
            var otherProperty = NewProperty(fixture.OtherOrganizationId);
            other.AddRange(new Organization("Second", "second", fixture.OtherOrganizationId), otherProperty);
            await other.SaveChangesAsync();
            fixture.OtherPropertyId = otherProperty.Id;
            return fixture;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
