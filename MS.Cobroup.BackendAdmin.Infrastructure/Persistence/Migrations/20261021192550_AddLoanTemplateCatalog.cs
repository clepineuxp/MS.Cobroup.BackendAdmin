using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanTemplateCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loan_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Classification = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    MaxLoanDays = table.Column<int>(type: "integer", nullable: false),
                    MaxPrincipalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AllowedPercentagesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_templates_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loan_templates_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loan_templates_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loan_template_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Classification = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MaxLoanDays = table.Column<int>(type: "integer", nullable: false),
                    MaxPrincipalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AllowedPercentagesJson = table.Column<string>(type: "jsonb", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_template_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_template_versions_loan_templates_LoanTemplateId",
                        column: x => x.LoanTemplateId,
                        principalTable: "loan_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loan_template_versions_users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_loan_template_versions_LoanTemplateId_Version",
                table: "loan_template_versions",
                columns: new[] { "LoanTemplateId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_template_versions_RecordedByUserId",
                table: "loan_template_versions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_templates_CreatedByUserId",
                table: "loan_templates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_templates_OrganizationId_NormalizedName",
                table: "loan_templates",
                columns: new[] { "OrganizationId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_templates_OrganizationId_Status_Classification",
                table: "loan_templates",
                columns: new[] { "OrganizationId", "Status", "Classification" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_templates_UpdatedByUserId",
                table: "loan_templates",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_template_versions");

            migrationBuilder.DropTable(
                name: "loan_templates");
        }
    }
}
