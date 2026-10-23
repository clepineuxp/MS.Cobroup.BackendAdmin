using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MS.Cobroup.BackendAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSigningKeyIdToRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Estrategia segura para datos existentes: purgar sesiones heredadas
            // para evitar atribuir silenciosamente 'k1' y exigir reautenticación segura.
            migrationBuilder.Sql("DELETE FROM refresh_tokens;");

            migrationBuilder.AddColumn<string>(
                name: "SigningKeyId",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_SigningKeyId",
                table: "refresh_tokens",
                column: "SigningKeyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_SigningKeyId",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "SigningKeyId",
                table: "refresh_tokens");
        }
    }
}
