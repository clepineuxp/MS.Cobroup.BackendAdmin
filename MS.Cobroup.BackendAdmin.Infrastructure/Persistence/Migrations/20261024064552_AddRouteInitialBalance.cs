using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteInitialBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InitialBalance",
                table: "routes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialBalance",
                table: "routes");
        }
    }
}
