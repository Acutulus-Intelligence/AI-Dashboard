using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemapTextVariantValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE dashboard_widgets
                SET "TextVariant" = CASE
                    WHEN "TextVariant" = 0 THEN 1
                    WHEN "TextVariant" = 1 THEN 2
                    ELSE "TextVariant"
                END
                WHERE "TextVariant" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE dashboard_widgets
                SET "TextVariant" = CASE
                    WHEN "TextVariant" = 1 THEN 0
                    WHEN "TextVariant" = 2 THEN 1
                    ELSE "TextVariant"
                END
                WHERE "TextVariant" IS NOT NULL;
                """);
        }
    }
}
