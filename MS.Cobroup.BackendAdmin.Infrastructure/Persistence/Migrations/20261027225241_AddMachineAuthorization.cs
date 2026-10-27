using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineAuthorization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "machine_authorization_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OtpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_authorization_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "machine_authorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AuthorizedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsIndefinite = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_authorizations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_machine_authorization_requests_OrganizationId_ExpiresAtUtc",
                table: "machine_authorization_requests",
                columns: new[] { "OrganizationId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_machine_authorizations_OrganizationId_RouteId_UserId_Machin~",
                table: "machine_authorizations",
                columns: new[] { "OrganizationId", "RouteId", "UserId", "MachineId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "machine_authorization_requests");

            migrationBuilder.DropTable(
                name: "machine_authorizations");
        }
    }
}
