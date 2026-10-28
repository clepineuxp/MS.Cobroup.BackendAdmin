using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MachineAuthorizationOtpSecret : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedOtp",
                table: "machine_authorization_requests",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedOtp",
                table: "machine_authorization_requests");
        }
    }
}
