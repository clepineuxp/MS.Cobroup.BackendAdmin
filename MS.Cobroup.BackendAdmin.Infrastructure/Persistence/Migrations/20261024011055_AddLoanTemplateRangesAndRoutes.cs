using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanTemplateRangesAndRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinPrincipalAmount",
                table: "loan_templates",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0.0001m);

            migrationBuilder.AddColumn<string>(
                name: "RouteIdsJson",
                table: "loan_templates",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<decimal>(
                name: "MinPrincipalAmount",
                table: "loan_template_versions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0.0001m);

            migrationBuilder.AddColumn<string>(
                name: "RouteIdsJson",
                table: "loan_template_versions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinPrincipalAmount",
                table: "loan_templates");

            migrationBuilder.DropColumn(
                name: "RouteIdsJson",
                table: "loan_templates");

            migrationBuilder.DropColumn(
                name: "MinPrincipalAmount",
                table: "loan_template_versions");

            migrationBuilder.DropColumn(
                name: "RouteIdsJson",
                table: "loan_template_versions");
        }
    }
}
