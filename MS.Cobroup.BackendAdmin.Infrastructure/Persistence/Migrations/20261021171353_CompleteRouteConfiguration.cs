using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteRouteConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaxActiveCustomersMode",
                table: "routes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Inherited");

            migrationBuilder.AddColumn<int>(
                name: "MaxActiveCustomersOverride",
                table: "routes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaxActiveLoansMode",
                table: "routes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Inherited");

            migrationBuilder.AddColumn<int>(
                name: "MaxActiveLoansOverride",
                table: "routes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "organization_settings",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MaxActiveCustomersPerRoute = table.Column<int>(type: "integer", nullable: true),
                    MaxActiveLoansPerRoute = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_settings", x => x.OrganizationId);
                    table.ForeignKey(
                        name: "FK_organization_settings_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_settings_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_settings_UpdatedByUserId",
                table: "organization_settings",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_settings");

            migrationBuilder.DropColumn(
                name: "MaxActiveCustomersMode",
                table: "routes");

            migrationBuilder.DropColumn(
                name: "MaxActiveCustomersOverride",
                table: "routes");

            migrationBuilder.DropColumn(
                name: "MaxActiveLoansMode",
                table: "routes");

            migrationBuilder.DropColumn(
                name: "MaxActiveLoansOverride",
                table: "routes");
        }
    }
}
