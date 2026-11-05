using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Demeure.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddResourceAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "contractors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlatformActorUserId",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "audit_logs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "property_manager_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_manager_assignments", x => x.Id);
                    table.UniqueConstraint("AK_property_manager_assignments_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_property_manager_assignments_organization_memberships_Organ~",
                        columns: x => new { x.OrganizationId, x.UserId },
                        principalTable: "organization_memberships",
                        principalColumns: new[] { "OrganizationId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_property_manager_assignments_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_property_manager_assignments_properties_OrganizationId_Prop~",
                        columns: x => new { x.OrganizationId, x.PropertyId },
                        principalTable: "properties",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contractors_OrganizationId_UserId",
                table: "contractors",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_PlatformActorUserId",
                table: "audit_logs",
                column: "PlatformActorUserId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_audit_logs_actor",
                table: "audit_logs",
                sql: "\"ActorUserId\" IS NULL OR \"PlatformActorUserId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_property_manager_assignments_OrganizationId_PropertyId",
                table: "property_manager_assignments",
                columns: new[] { "OrganizationId", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_property_manager_assignments_OrganizationId_UserId_Property~",
                table: "property_manager_assignments",
                columns: new[] { "OrganizationId", "UserId", "PropertyId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_users_PlatformActorUserId",
                table: "audit_logs",
                column: "PlatformActorUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_contractors_organization_memberships_OrganizationId_UserId",
                table: "contractors",
                columns: new[] { "OrganizationId", "UserId" },
                principalTable: "organization_memberships",
                principalColumns: new[] { "OrganizationId", "UserId" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql(PostgreSqlOrganizationSecurity.EnableSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PostgreSqlOrganizationSecurity.DisableSql);
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_users_PlatformActorUserId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_contractors_organization_memberships_OrganizationId_UserId",
                table: "contractors");

            migrationBuilder.DropTable(
                name: "property_manager_assignments");

            migrationBuilder.DropIndex(
                name: "IX_contractors_OrganizationId_UserId",
                table: "contractors");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_PlatformActorUserId",
                table: "audit_logs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_audit_logs_actor",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "contractors");

            migrationBuilder.DropColumn(
                name: "PlatformActorUserId",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "audit_logs");
        }
    }
}
