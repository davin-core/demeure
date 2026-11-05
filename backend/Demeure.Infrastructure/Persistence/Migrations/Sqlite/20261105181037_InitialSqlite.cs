using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demeure.Infrastructure.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                    table.CheckConstraint("ck_organizations_slug_lowercase", "\"Slug\" = lower(\"Slug\")");
                    table.CheckConstraint("ck_organizations_status", "\"Status\" IN ('Active', 'Suspended', 'Archived')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IdentityIssuer = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    IdentitySubject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.CheckConstraint("ck_users_status", "\"Status\" IN ('Active', 'Disabled')");
                });

            migrationBuilder.CreateTable(
                name: "contractors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Trade = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contractors", x => x.Id);
                    table.UniqueConstraint("AK_contractors_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_contractors_status", "\"Status\" IN ('Active', 'Inactive', 'Archived')");
                    table.ForeignKey(
                        name: "FK_contractors_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AddressLine1 = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    AddressLine2 = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_properties", x => x.Id);
                    table.UniqueConstraint("AK_properties_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_properties_country_code", "length(\"CountryCode\") = 2 AND \"CountryCode\" = upper(\"CountryCode\")");
                    table.CheckConstraint("ck_properties_status", "\"Status\" IN ('Active', 'Inactive', 'Archived')");
                    table.ForeignKey(
                        name: "FK_properties_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "organization_memberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_memberships", x => x.Id);
                    table.UniqueConstraint("AK_organization_memberships_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.UniqueConstraint("AK_organization_memberships_OrganizationId_UserId", x => new { x.OrganizationId, x.UserId });
                    table.CheckConstraint("ck_organization_memberships_role", "\"Role\" IN ('Owner', 'Administrator', 'Manager', 'Member')");
                    table.CheckConstraint("ck_organization_memberships_status", "\"Status\" IN ('Active', 'Suspended', 'Removed')");
                    table.ForeignKey(
                        name: "FK_organization_memberships_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_memberships_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Bedrooms = table.Column<int>(type: "INTEGER", nullable: false),
                    Bathrooms = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.Id);
                    table.UniqueConstraint("AK_units_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.UniqueConstraint("AK_units_OrganizationId_PropertyId_Id", x => new { x.OrganizationId, x.PropertyId, x.Id });
                    table.CheckConstraint("ck_units_rooms", "\"Bedrooms\" >= 0 AND \"Bathrooms\" >= 0");
                    table.CheckConstraint("ck_units_status", "\"Status\" IN ('Available', 'Occupied', 'Unavailable', 'Archived')");
                    table.ForeignKey(
                        name: "FK_units_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_units_properties_OrganizationId_PropertyId",
                        columns: x => new { x.OrganizationId, x.PropertyId },
                        principalTable: "properties",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TraceId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.UniqueConstraint("AK_audit_logs_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_audit_logs_organization_memberships_OrganizationId_ActorUserId",
                        columns: x => new { x.OrganizationId, x.ActorUserId },
                        principalTable: "organization_memberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_logs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SentAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ReadAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.UniqueConstraint("AK_notifications_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_notifications_channel", "\"Channel\" IN ('InApp', 'Email')");
                    table.CheckConstraint("ck_notifications_status", "\"Status\" IN ('Pending', 'Sent', 'Failed')");
                    table.ForeignKey(
                        name: "FK_notifications_organization_memberships_OrganizationId_RecipientUserId",
                        columns: x => new { x.OrganizationId, x.RecipientUserId },
                        principalTable: "organization_memberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notifications_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                    table.UniqueConstraint("AK_tenants_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_tenants_status", "\"Status\" IN ('Active', 'Inactive', 'Archived')");
                    table.ForeignKey(
                        name: "FK_tenants_organization_memberships_OrganizationId_UserId",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalTable: "organization_memberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tenants_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContractorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    IncurredDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.Id);
                    table.UniqueConstraint("AK_expenses_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_expenses_amount", "\"Amount\" > 0");
                    table.CheckConstraint("ck_expenses_currency", "length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                    table.CheckConstraint("ck_expenses_status", "\"Status\" IN ('Draft', 'Approved', 'Paid', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_expenses_contractors_OrganizationId_ContractorId",
                        columns: x => new { x.OrganizationId, x.ContractorId },
                        principalTable: "contractors",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expenses_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expenses_properties_OrganizationId_PropertyId",
                        columns: x => new { x.OrganizationId, x.PropertyId },
                        principalTable: "properties",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expenses_units_OrganizationId_PropertyId_UnitId",
                        columns: x => new { x.OrganizationId, x.PropertyId, x.UnitId },
                        principalTable: "units",
                        principalColumns: new[] { "OrganizationId", "PropertyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContractorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_maintenance_requests", x => x.Id);
                    table.UniqueConstraint("AK_maintenance_requests_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_maintenance_requests_priority", "\"Priority\" IN ('Low', 'Normal', 'High', 'Emergency')");
                    table.CheckConstraint("ck_maintenance_requests_status", "\"Status\" IN ('Open', 'Assigned', 'InProgress', 'Completed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_maintenance_requests_contractors_OrganizationId_ContractorId",
                        columns: x => new { x.OrganizationId, x.ContractorId },
                        principalTable: "contractors",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_requests_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_requests_properties_OrganizationId_PropertyId",
                        columns: x => new { x.OrganizationId, x.PropertyId },
                        principalTable: "properties",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_requests_tenants_OrganizationId_TenantId",
                        columns: x => new { x.OrganizationId, x.TenantId },
                        principalTable: "tenants",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_maintenance_requests_units_OrganizationId_PropertyId_UnitId",
                        columns: x => new { x.OrganizationId, x.PropertyId, x.UnitId },
                        principalTable: "units",
                        principalColumns: new[] { "OrganizationId", "PropertyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rental_applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmittedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DecidedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rental_applications", x => x.Id);
                    table.UniqueConstraint("AK_rental_applications_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.UniqueConstraint("AK_rental_applications_OrganizationId_UnitId_TenantId_Id", x => new { x.OrganizationId, x.UnitId, x.TenantId, x.Id });
                    table.CheckConstraint("ck_applications_decision_time", "\"DecidedAt\" IS NULL OR (\"SubmittedAt\" IS NOT NULL AND \"DecidedAt\" >= \"SubmittedAt\")");
                    table.CheckConstraint("ck_rental_applications_status", "\"Status\" IN ('Draft', 'Submitted', 'UnderReview', 'Approved', 'Rejected', 'Withdrawn')");
                    table.ForeignKey(
                        name: "FK_rental_applications_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_applications_tenants_OrganizationId_TenantId",
                        columns: x => new { x.OrganizationId, x.TenantId },
                        principalTable: "tenants",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_applications_units_OrganizationId_UnitId",
                        columns: x => new { x.OrganizationId, x.UnitId },
                        principalTable: "units",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RentalApplicationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    MonthlyRent = table.Column<long>(type: "INTEGER", precision: 18, scale: 2, nullable: false),
                    SecurityDeposit = table.Column<long>(type: "INTEGER", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leases", x => x.Id);
                    table.UniqueConstraint("AK_leases_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_leases_amounts", "\"MonthlyRent\" > 0 AND \"SecurityDeposit\" >= 0");
                    table.CheckConstraint("ck_leases_currency", "length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                    table.CheckConstraint("ck_leases_dates", "\"EndDate\" > \"StartDate\"");
                    table.CheckConstraint("ck_leases_status", "\"Status\" IN ('Draft', 'Active', 'Ended', 'Terminated', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_leases_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leases_rental_applications_OrganizationId_UnitId_TenantId_RentalApplicationId",
                        columns: x => new { x.OrganizationId, x.UnitId, x.TenantId, x.RentalApplicationId },
                        principalTable: "rental_applications",
                        principalColumns: new[] { "OrganizationId", "UnitId", "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leases_tenants_OrganizationId_TenantId",
                        columns: x => new { x.OrganizationId, x.TenantId },
                        principalTable: "tenants",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leases_units_OrganizationId_UnitId",
                        columns: x => new { x.OrganizationId, x.UnitId },
                        principalTable: "units",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LeaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MaintenanceRequestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 127, nullable: false),
                    StorageKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.UniqueConstraint("AK_documents_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_documents_single_parent", "(CASE WHEN \"PropertyId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"LeaseId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"MaintenanceRequestId\" IS NULL THEN 0 ELSE 1 END) <= 1");
                    table.CheckConstraint("ck_documents_size", "\"SizeBytes\" >= 0");
                    table.CheckConstraint("ck_documents_status", "\"Status\" IN ('Available', 'Archived')");
                    table.ForeignKey(
                        name: "FK_documents_leases_OrganizationId_LeaseId",
                        columns: x => new { x.OrganizationId, x.LeaseId },
                        principalTable: "leases",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_maintenance_requests_OrganizationId_MaintenanceRequestId",
                        columns: x => new { x.OrganizationId, x.MaintenanceRequestId },
                        principalTable: "maintenance_requests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_organization_memberships_OrganizationId_UploadedByUserId",
                        columns: x => new { x.OrganizationId, x.UploadedByUserId },
                        principalTable: "organization_memberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_properties_OrganizationId_PropertyId",
                        columns: x => new { x.OrganizationId, x.PropertyId },
                        principalTable: "properties",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LeaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PaidAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.UniqueConstraint("AK_payments_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("ck_payments_amount", "\"Amount\" > 0");
                    table.CheckConstraint("ck_payments_currency", "length(\"Currency\") = 3 AND \"Currency\" = upper(\"Currency\")");
                    table.CheckConstraint("ck_payments_status", "\"Status\" IN ('Pending', 'Completed', 'Failed', 'Cancelled', 'Refunded')");
                    table.ForeignKey(
                        name: "FK_payments_leases_OrganizationId_LeaseId",
                        columns: x => new { x.OrganizationId, x.LeaseId },
                        principalTable: "leases",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payments_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OrganizationId_ActorUserId",
                table: "audit_logs",
                columns: new[] { "OrganizationId", "ActorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OrganizationId_CreatedAt",
                table: "audit_logs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OrganizationId_ResourceType_ResourceId",
                table: "audit_logs",
                columns: new[] { "OrganizationId", "ResourceType", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_contractors_OrganizationId_Status",
                table: "contractors",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_OrganizationId_LeaseId",
                table: "documents",
                columns: new[] { "OrganizationId", "LeaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_OrganizationId_MaintenanceRequestId",
                table: "documents",
                columns: new[] { "OrganizationId", "MaintenanceRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_OrganizationId_PropertyId",
                table: "documents",
                columns: new[] { "OrganizationId", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_OrganizationId_StorageKey",
                table: "documents",
                columns: new[] { "OrganizationId", "StorageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_OrganizationId_UploadedByUserId",
                table: "documents",
                columns: new[] { "OrganizationId", "UploadedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_OrganizationId_ContractorId",
                table: "expenses",
                columns: new[] { "OrganizationId", "ContractorId" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_OrganizationId_PropertyId_IncurredDate",
                table: "expenses",
                columns: new[] { "OrganizationId", "PropertyId", "IncurredDate" });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_OrganizationId_PropertyId_UnitId",
                table: "expenses",
                columns: new[] { "OrganizationId", "PropertyId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_leases_OrganizationId_RentalApplicationId",
                table: "leases",
                columns: new[] { "OrganizationId", "RentalApplicationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leases_OrganizationId_TenantId",
                table: "leases",
                columns: new[] { "OrganizationId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_leases_OrganizationId_UnitId_Status",
                table: "leases",
                columns: new[] { "OrganizationId", "UnitId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_leases_OrganizationId_UnitId_TenantId_RentalApplicationId",
                table: "leases",
                columns: new[] { "OrganizationId", "UnitId", "TenantId", "RentalApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_OrganizationId_ContractorId",
                table: "maintenance_requests",
                columns: new[] { "OrganizationId", "ContractorId" });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_OrganizationId_PropertyId_UnitId",
                table: "maintenance_requests",
                columns: new[] { "OrganizationId", "PropertyId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_OrganizationId_Status_Priority",
                table: "maintenance_requests",
                columns: new[] { "OrganizationId", "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_maintenance_requests_OrganizationId_TenantId",
                table: "maintenance_requests",
                columns: new[] { "OrganizationId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_OrganizationId_RecipientUserId_ReadAt_CreatedAt",
                table: "notifications",
                columns: new[] { "OrganizationId", "RecipientUserId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_OrganizationId_Status_CreatedAt",
                table: "notifications",
                columns: new[] { "OrganizationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_OrganizationId_Status",
                table: "organization_memberships",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_UserId",
                table: "organization_memberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_Slug",
                table: "organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrganizationId_LeaseId",
                table: "payments",
                columns: new[] { "OrganizationId", "LeaseId" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrganizationId_Status_DueDate",
                table: "payments",
                columns: new[] { "OrganizationId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_properties_OrganizationId_Status",
                table: "properties",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_rental_applications_OrganizationId_Status_CreatedAt",
                table: "rental_applications",
                columns: new[] { "OrganizationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_rental_applications_OrganizationId_TenantId",
                table: "rental_applications",
                columns: new[] { "OrganizationId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_OrganizationId_LastName_FirstName",
                table: "tenants",
                columns: new[] { "OrganizationId", "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_OrganizationId_UserId",
                table: "tenants",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_units_OrganizationId_PropertyId_Number",
                table: "units",
                columns: new[] { "OrganizationId", "PropertyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_units_OrganizationId_Status",
                table: "units",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_users_IdentityIssuer_IdentitySubject",
                table: "users",
                columns: new[] { "IdentityIssuer", "IdentitySubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_NormalizedEmail",
                table: "users",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "maintenance_requests");

            migrationBuilder.DropTable(
                name: "leases");

            migrationBuilder.DropTable(
                name: "contractors");

            migrationBuilder.DropTable(
                name: "rental_applications");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropTable(
                name: "organization_memberships");

            migrationBuilder.DropTable(
                name: "properties");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
