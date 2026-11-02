using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCompanyInviteEmailIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyInvites_CompanyId",
                table: "CompanyInvites");

            // Emails are matched case-insensitively at runtime, so normalise the
            // stored value and collapse any duplicates (keeping the newest)
            // before the unique (CompanyId, Email) index is created.
            migrationBuilder.Sql("UPDATE \"CompanyInvites\" SET \"Email\" = LOWER(\"Email\");");

            migrationBuilder.Sql(
                "DELETE FROM \"CompanyInvites\" a USING \"CompanyInvites\" b " +
                "WHERE a.\"CompanyId\" = b.\"CompanyId\" AND a.\"Email\" = b.\"Email\" " +
                "AND (a.\"CreatedAt\" < b.\"CreatedAt\" " +
                "OR (a.\"CreatedAt\" = b.\"CreatedAt\" AND a.ctid < b.ctid));");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvites_CompanyId_Email",
                table: "CompanyInvites",
                columns: new[] { "CompanyId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyInvites_CompanyId_Email",
                table: "CompanyInvites");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvites_CompanyId",
                table: "CompanyInvites",
                column: "CompanyId");
        }
    }
}
