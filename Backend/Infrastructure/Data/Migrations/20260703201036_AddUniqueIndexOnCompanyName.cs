using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnCompanyName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Companies" c
                SET "Name" = c."Name" || ' (' || dup.rn || ')'
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "Name" ORDER BY "Id") AS rn
                    FROM "Companies"
                ) dup
                WHERE c."Id" = dup."Id" AND dup.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                table: "Companies",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_Name",
                table: "Companies");
        }
    }
}
