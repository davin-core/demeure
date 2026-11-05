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
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Demeure.Infrastructure.Persistence;

internal static class DemeureModelConfiguration
{
    internal static void Configure(ModelBuilder model, bool sqlite)
    {
        var organizations = Root<Organization>(model, "organizations");
        Text(organizations, nameof(Organization.Name), 200);
        Text(organizations, nameof(Organization.Slug), 100);
        organizations.HasIndex(entity => entity.Slug).IsUnique();
        organizations.ToTable(table => table.HasCheckConstraint("ck_organizations_slug_lowercase", "\"Slug\" = lower(\"Slug\")"));

        var users = Root<User>(model, "users");
        Text(users, nameof(User.IdentityIssuer), 300);
        Text(users, nameof(User.IdentitySubject), 200);
        Text(users, nameof(User.Email), 254);
        Text(users, nameof(User.NormalizedEmail), 254);
        Text(users, nameof(User.DisplayName), 200);
        users.HasIndex(entity => new { entity.IdentityIssuer, entity.IdentitySubject }).IsUnique();
        users.HasIndex(entity => entity.NormalizedEmail).IsUnique();

        var memberships = Scoped<OrganizationMembership>(model, "organization_memberships");
        memberships.HasAlternateKey(entity => new { entity.OrganizationId, entity.UserId });
        memberships.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        memberships.HasIndex(entity => new { entity.OrganizationId, entity.Status });

        var properties = Scoped<Property>(model, "properties");
        Text(properties, nameof(Property.Name), 200);
        Text(properties, nameof(Property.AddressLine1), 300);
        Text(properties, nameof(Property.AddressLine2), 300, false);
        Text(properties, nameof(Property.City), 100);
        Text(properties, nameof(Property.Region), 100, false);
        Text(properties, nameof(Property.PostalCode), 20);
        Text(properties, nameof(Property.CountryCode), 2);
        properties.HasIndex(entity => new { entity.OrganizationId, entity.Status });
        properties.ToTable(table => table.HasCheckConstraint("ck_properties_country_code", "length(\"CountryCode\") = 2 AND \"CountryCode\" = upper(\"CountryCode\")"));

        var assignments = Scoped<PropertyManagerAssignment>(model, "property_manager_assignments");
        Related<PropertyManagerAssignment, Property>(assignments, nameof(PropertyManagerAssignment.PropertyId));
        MemberReference(assignments, nameof(PropertyManagerAssignment.UserId));
        assignments.HasIndex(entity => new { entity.OrganizationId, entity.UserId, entity.PropertyId }).IsUnique();

        var units = Scoped<Unit>(model, "units");
        Related<Unit, Property>(units, nameof(Unit.PropertyId));
        Text(units, nameof(Unit.Number), 50);
        units.HasIndex(entity => new { entity.OrganizationId, entity.PropertyId, entity.Number }).IsUnique();
        units.HasAlternateKey(entity => new { entity.OrganizationId, entity.PropertyId, entity.Id });
        units.HasIndex(entity => new { entity.OrganizationId, entity.Status });
        units.ToTable(table => table.HasCheckConstraint("ck_units_rooms", "\"Bedrooms\" >= 0 AND \"Bathrooms\" >= 0"));

        var tenants = Scoped<Tenant>(model, "tenants");
        Text(tenants, nameof(Tenant.FirstName), 100);
        Text(tenants, nameof(Tenant.LastName), 100);
        Text(tenants, nameof(Tenant.Email), 254, false);
        Text(tenants, nameof(Tenant.Phone), 30, false);
        tenants.HasOne<OrganizationMembership>().WithMany()
            .HasForeignKey(entity => new { entity.OrganizationId, entity.UserId })
            .HasPrincipalKey(entity => new { entity.OrganizationId, entity.UserId })
            .OnDelete(DeleteBehavior.Restrict);
        tenants.HasIndex(entity => new { entity.OrganizationId, entity.UserId }).IsUnique();
        tenants.HasIndex(entity => new { entity.OrganizationId, entity.LastName, entity.FirstName });

        var applications = Scoped<RentalApplication>(model, "rental_applications");
        Related<RentalApplication, Unit>(applications, nameof(RentalApplication.UnitId));
        Related<RentalApplication, Tenant>(applications, nameof(RentalApplication.TenantId));
        applications.HasAlternateKey(entity => new { entity.OrganizationId, entity.UnitId, entity.TenantId, entity.Id });
        applications.HasIndex(entity => new { entity.OrganizationId, entity.Status, entity.CreatedAt });
        applications.ToTable(table => table.HasCheckConstraint("ck_applications_decision_time", "\"DecidedAt\" IS NULL OR (\"SubmittedAt\" IS NOT NULL AND \"DecidedAt\" >= \"SubmittedAt\")"));

        var leases = Scoped<Lease>(model, "leases");
        Related<Lease, Unit>(leases, nameof(Lease.UnitId));
        Related<Lease, Tenant>(leases, nameof(Lease.TenantId));
        leases.HasOne<RentalApplication>().WithMany()
            .HasForeignKey(entity => new { entity.OrganizationId, entity.UnitId, entity.TenantId, entity.RentalApplicationId })
            .HasPrincipalKey(entity => new { entity.OrganizationId, entity.UnitId, entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
        leases.HasIndex(entity => new { entity.OrganizationId, entity.RentalApplicationId }).IsUnique();
        leases.HasIndex(entity => new { entity.OrganizationId, entity.UnitId, entity.Status });
        Money(leases, nameof(Lease.MonthlyRent));
        Money(leases, nameof(Lease.SecurityDeposit));
        Currency(leases);
        leases.ToTable(table =>
        {
            table.HasCheckConstraint("ck_leases_dates", "\"EndDate\" > \"StartDate\"");
            table.HasCheckConstraint("ck_leases_amounts", "\"MonthlyRent\" > 0 AND \"SecurityDeposit\" >= 0");
        });

        var payments = Scoped<Payment>(model, "payments");
        Related<Payment, Lease>(payments, nameof(Payment.LeaseId));
        Money(payments, nameof(Payment.Amount));
        Currency(payments);
        Text(payments, nameof(Payment.Reference), 200, false);
        payments.HasIndex(entity => new { entity.OrganizationId, entity.Status, entity.DueDate });
        payments.ToTable(table => table.HasCheckConstraint("ck_payments_amount", "\"Amount\" > 0"));

        var contractors = Scoped<Contractor>(model, "contractors");
        MemberReference(contractors, nameof(Contractor.UserId));
        contractors.HasIndex(entity => new { entity.OrganizationId, entity.UserId }).IsUnique();
        Text(contractors, nameof(Contractor.Name), 200);
        Text(contractors, nameof(Contractor.Email), 254, false);
        Text(contractors, nameof(Contractor.Phone), 30, false);
        Text(contractors, nameof(Contractor.Trade), 100, false);
        contractors.HasIndex(entity => new { entity.OrganizationId, entity.Status });

        var expenses = Scoped<Expense>(model, "expenses");
        Related<Expense, Property>(expenses, nameof(Expense.PropertyId));
        UnitWithinProperty(expenses);
        Related<Expense, Contractor>(expenses, nameof(Expense.ContractorId));
        Text(expenses, nameof(Expense.Description), 1000);
        Money(expenses, nameof(Expense.Amount));
        Currency(expenses);
        expenses.HasIndex(entity => new { entity.OrganizationId, entity.PropertyId, entity.IncurredDate });
        expenses.ToTable(table => table.HasCheckConstraint("ck_expenses_amount", "\"Amount\" > 0"));

        var maintenance = Scoped<MaintenanceRequest>(model, "maintenance_requests");
        Related<MaintenanceRequest, Property>(maintenance, nameof(MaintenanceRequest.PropertyId));
        UnitWithinProperty(maintenance);
        Related<MaintenanceRequest, Tenant>(maintenance, nameof(MaintenanceRequest.TenantId));
        Related<MaintenanceRequest, Contractor>(maintenance, nameof(MaintenanceRequest.ContractorId));
        Text(maintenance, nameof(MaintenanceRequest.Title), 200);
        Text(maintenance, nameof(MaintenanceRequest.Description), 4000);
        maintenance.HasIndex(entity => new { entity.OrganizationId, entity.Status, entity.Priority });

        var documents = Scoped<Document>(model, "documents");
        Related<Document, Property>(documents, nameof(Document.PropertyId));
        Related<Document, Lease>(documents, nameof(Document.LeaseId));
        Related<Document, MaintenanceRequest>(documents, nameof(Document.MaintenanceRequestId));
        MemberReference(documents, nameof(Document.UploadedByUserId));
        Text(documents, nameof(Document.FileName), 255);
        Text(documents, nameof(Document.ContentType), 127);
        Text(documents, nameof(Document.StorageKey), 500);
        documents.HasIndex(entity => new { entity.OrganizationId, entity.StorageKey }).IsUnique();
        documents.ToTable(table =>
        {
            table.HasCheckConstraint("ck_documents_size", "\"SizeBytes\" >= 0");
            table.HasCheckConstraint("ck_documents_single_parent",
                "(CASE WHEN \"PropertyId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"LeaseId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"MaintenanceRequestId\" IS NULL THEN 0 ELSE 1 END) <= 1");
        });

        var notifications = Scoped<Notification>(model, "notifications");
        MemberReference(notifications, nameof(Notification.RecipientUserId));
        Text(notifications, nameof(Notification.Title), 200);
        Text(notifications, nameof(Notification.Body), 4000);
        notifications.HasIndex(entity => new { entity.OrganizationId, entity.RecipientUserId, entity.ReadAt, entity.CreatedAt });
        notifications.HasIndex(entity => new { entity.OrganizationId, entity.Status, entity.CreatedAt });

        var audits = Scoped<AuditLog>(model, "audit_logs");
        MemberReference(audits, nameof(AuditLog.ActorUserId));
        audits.HasOne<User>().WithMany().HasForeignKey(entity => entity.PlatformActorUserId).OnDelete(DeleteBehavior.Restrict);
        Text(audits, nameof(AuditLog.Reason), 500, false);
        audits.ToTable(table => table.HasCheckConstraint("ck_audit_logs_actor",
            "\"ActorUserId\" IS NULL OR \"PlatformActorUserId\" IS NULL"));
        Text(audits, nameof(AuditLog.Action), 100);
        Text(audits, nameof(AuditLog.ResourceType), 100);
        Text(audits, nameof(AuditLog.TraceId), 64, false);
        audits.HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt });
        audits.HasIndex(entity => new { entity.OrganizationId, entity.ResourceType, entity.ResourceId });

        foreach (var entity in model.Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(entity.ClrType)) continue;
            foreach (var property in entity.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type.IsEnum)
                {
                    model.Entity(entity.ClrType).Property(property.Name).HasConversion<string>().HasMaxLength(32);
                    var values = string.Join(", ", Enum.GetNames(type).Select(value => $"'{value}'"));
                    model.Entity(entity.ClrType).ToTable(table => table.HasCheckConstraint(
                        $"ck_{entity.GetTableName()}_{property.Name.ToLowerInvariant()}",
                        $"\"{property.Name}\" IN ({values})"));
                }
                if (type == typeof(DateTimeOffset) && sqlite)
                {
                    model.Entity(entity.ClrType).Property(property.Name).HasConversion(
                        new ValueConverter<DateTimeOffset, long>(
                            value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero)));
                }
                if (type == typeof(decimal) && sqlite)
                {
                    model.Entity(entity.ClrType).Property(property.Name).HasConversion(
                        new ValueConverter<decimal, long>(value => checked((long)(value * 100m)), value => value / 100m));
                }
            }
        }
    }

    private static EntityTypeBuilder<T> Root<T>(ModelBuilder model, string table) where T : Entity
    {
        var entity = model.Entity<T>().ToTable(table);
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Id).ValueGeneratedNever();
        entity.Property(value => value.CreatedAt).IsRequired();
        entity.Property(value => value.UpdatedAt).IsRequired();
        return entity;
    }

    private static EntityTypeBuilder<T> Scoped<T>(ModelBuilder model, string table) where T : OrganizationEntity
    {
        var entity = Root<T>(model, table);
        entity.HasAlternateKey(value => new { value.OrganizationId, value.Id });
        entity.HasOne<Organization>().WithMany().HasForeignKey(value => value.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        return entity;
    }

    private static void Related<T, TParent>(EntityTypeBuilder<T> entity, string foreignKey)
        where T : OrganizationEntity where TParent : OrganizationEntity =>
        entity.HasOne<TParent>().WithMany()
            .HasForeignKey(nameof(OrganizationEntity.OrganizationId), foreignKey)
            .HasPrincipalKey(nameof(OrganizationEntity.OrganizationId), nameof(Entity.Id))
            .OnDelete(DeleteBehavior.Restrict);

    private static void UnitWithinProperty<T>(EntityTypeBuilder<T> entity) where T : OrganizationEntity =>
        entity.HasOne<Unit>().WithMany()
            .HasForeignKey(nameof(OrganizationEntity.OrganizationId), "PropertyId", "UnitId")
            .HasPrincipalKey(nameof(OrganizationEntity.OrganizationId), nameof(Unit.PropertyId), nameof(Entity.Id))
            .OnDelete(DeleteBehavior.Restrict);

    private static void MemberReference<T>(EntityTypeBuilder<T> entity, string userId) where T : OrganizationEntity =>
        entity.HasOne<OrganizationMembership>().WithMany()
            .HasForeignKey(nameof(OrganizationEntity.OrganizationId), userId)
            .HasPrincipalKey(nameof(OrganizationEntity.OrganizationId), nameof(OrganizationMembership.UserId))
            .OnDelete(DeleteBehavior.Restrict);

    private static void Text<T>(EntityTypeBuilder<T> entity, string name, int length, bool required = true) where T : Entity =>
        entity.Property<string>(name).HasMaxLength(length).IsRequired(required);

    private static void Money<T>(EntityTypeBuilder<T> entity, string name) where T : Entity =>
        entity.Property<decimal>(name).HasPrecision(18, 2);

    private static void Currency<T>(EntityTypeBuilder<T> entity) where T : Entity
    {
        Text(entity, "Currency", 3);
        entity.ToTable(table => table.HasCheckConstraint(
            $"ck_{entity.Metadata.GetTableName()}_currency", "length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")"));
    }
}
