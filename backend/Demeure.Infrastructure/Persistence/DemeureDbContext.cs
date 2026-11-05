using System.ComponentModel.DataAnnotations;
using Demeure.Application.Common;
using Demeure.Application.Common.Exceptions;
using Demeure.Domain.Common;
using Demeure.Domain.Modules.Applications.Entities;
using Demeure.Domain.Modules.Contractors.Entities;
using Demeure.Domain.Modules.Documents.Entities;
using Demeure.Domain.Modules.Expenses.Entities;
using Demeure.Domain.Modules.Leases.Entities;
using Demeure.Domain.Modules.Maintenance.Entities;
using Demeure.Domain.Modules.Notifications.Entities;
using Demeure.Domain.Modules.Organizations.Entities;
using Demeure.Domain.Modules.Payments.Entities;
using Demeure.Domain.Modules.Properties.Entities;
using Demeure.Domain.Modules.Tenants.Entities;
using Demeure.Domain.Modules.Units.Entities;
using Demeure.Domain.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Demeure.Infrastructure.Authentication;

namespace Demeure.Infrastructure.Persistence;

public class DemeureDbContext(
    DbContextOptions options,
    IOrganizationContext organizationContext) : IdentityDbContext<IdentityAccount, IdentityRole<Guid>, Guid>(options)
{
    public Guid CurrentOrganizationId => organizationContext.OrganizationId is { } id && id != Guid.Empty
        ? id
        : throw new ForbiddenException("An authorized organization context is required for persistence.");

    public DbSet<Organization> Organizations => Set<Organization>();
    public new DbSet<User> Users => Set<User>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyManagerAssignment> PropertyManagerAssignments => Set<PropertyManagerAssignment>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RentalApplication> RentalApplications => Set<RentalApplication>();
    public DbSet<Lease> Leases => Set<Lease>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureIdentity(modelBuilder);
        DemeureModelConfiguration.Configure(modelBuilder, Database.IsSqlite());
        modelBuilder.Entity<Organization>().HasQueryFilter(entity => entity.Id == CurrentOrganizationId);
        Scope<OrganizationMembership>(modelBuilder);
        Scope<Property>(modelBuilder);
        Scope<PropertyManagerAssignment>(modelBuilder);
        Scope<Unit>(modelBuilder);
        Scope<Tenant>(modelBuilder);
        Scope<RentalApplication>(modelBuilder);
        Scope<Lease>(modelBuilder);
        Scope<Payment>(modelBuilder);
        Scope<Expense>(modelBuilder);
        Scope<MaintenanceRequest>(modelBuilder);
        Scope<Contractor>(modelBuilder);
        Scope<Document>(modelBuilder);
        Scope<Notification>(modelBuilder);
        Scope<AuditLog>(modelBuilder);
    }

    private static void ConfigureIdentity(ModelBuilder model)
    {
        model.Entity<IdentityAccount>().ToTable("identity_accounts");
        model.Entity<IdentityRole<Guid>>().ToTable("identity_roles");
        model.Entity<IdentityUserRole<Guid>>().ToTable("identity_user_roles");
        model.Entity<IdentityUserClaim<Guid>>().ToTable("identity_user_claims");
        model.Entity<IdentityUserLogin<Guid>>().ToTable("identity_user_logins");
        model.Entity<IdentityRoleClaim<Guid>>().ToTable("identity_role_claims");
        model.Entity<IdentityUserToken<Guid>>().ToTable("identity_user_tokens");
        model.Entity<IdentityAccount>().HasOne<User>().WithOne().HasForeignKey<IdentityAccount>(user => user.Id)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<IdentityAccount>().Property(user => user.Email).HasMaxLength(254);
        model.Entity<IdentityAccount>().Property(user => user.NormalizedEmail).HasMaxLength(254);
        model.Entity<IdentityAccount>().Property(user => user.UserName).HasMaxLength(254);
        model.Entity<IdentityAccount>().Property(user => user.NormalizedUserName).HasMaxLength(254);
        model.Entity<IdentityAccount>().Property(user => user.PasswordHash).HasMaxLength(1024);
    }

    private void Scope<T>(ModelBuilder modelBuilder) where T : OrganizationEntity =>
        modelBuilder.Entity<T>().HasQueryFilter(entity => entity.OrganizationId == CurrentOrganizationId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareChanges()
    {
        ChangeTracker.DetectChanges();
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is OrganizationEntity scoped)
            {
                if (scoped.OrganizationId != CurrentOrganizationId ||
                    entry.State != EntityState.Added &&
                    entry.Property(nameof(OrganizationEntity.OrganizationId)).OriginalValue is Guid original &&
                    original != CurrentOrganizationId)
                {
                    throw new ForbiddenException("Cannot write data outside the authorized organization.");
                }
            }
            if (entry.Entity is Organization organization && organization.Id != CurrentOrganizationId)
            {
                throw new ForbiddenException("Cannot write another organization.");
            }
            if (entry.Entity is AuditLog && entry.State != EntityState.Added)
            {
                throw new InvalidOperationException("Audit logs are append-only.");
            }
            if (entry.State == EntityState.Deleted) continue;
            if (entry.Entity.Id == Guid.Empty) throw new ValidationException("Entity IDs must not be empty.");
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
            else entry.Property(nameof(Entity.CreatedAt)).IsModified = false;
            entry.Entity.UpdatedAt = now;

            foreach (var property in entry.Properties)
            {
                var value = property.CurrentValue;
                if (value is DateTimeOffset timestamp) property.CurrentValue = timestamp.ToUniversalTime();
                if (value is string text &&
                    (!property.Metadata.IsNullable && string.IsNullOrWhiteSpace(text) ||
                     property.Metadata.GetMaxLength() is { } max && text.Length > max))
                {
                    throw new ValidationException($"{property.Metadata.Name} is empty or exceeds its maximum length.");
                }
                if (value is null && !property.Metadata.IsNullable)
                    throw new ValidationException($"{property.Metadata.Name} is required.");
                if (value is Enum enumeration && !Enum.IsDefined(enumeration.GetType(), enumeration))
                    throw new ValidationException($"{property.Metadata.Name} has an invalid status.");
                if (value is decimal amount && property.Metadata.GetScale() == 2 &&
                    (decimal.Round(amount, 2) != amount || decimal.Abs(amount) > 9999999999999999.99m))
                    throw new ValidationException($"{property.Metadata.Name} must fit decimal(18,2) without rounding.");
                if (property.Metadata.Name == "Currency" && value is string currency &&
                    (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z')))
                    throw new ValidationException("Currency must be a three-letter uppercase code.");
            }
        }
    }
}
