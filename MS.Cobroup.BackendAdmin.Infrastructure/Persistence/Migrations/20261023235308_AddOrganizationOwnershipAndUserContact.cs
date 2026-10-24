using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationOwnershipAndUserContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "users",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<bool>(
                name: "IsOwner",
                table: "organization_memberships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE users SET \"UpdatedAtUtc\" = \"CreatedAtUtc\";");

            migrationBuilder.CreateIndex(
                name: "IX_organization_memberships_OrganizationId_IsOwner",
                table: "organization_memberships",
                columns: new[] { "OrganizationId", "IsOwner" },
                unique: true,
                filter: "\"IsOwner\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_organization_memberships_OrganizationId_IsOwner",
                table: "organization_memberships");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "users");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "IsOwner",
                table: "organization_memberships");

        }
    }
}
