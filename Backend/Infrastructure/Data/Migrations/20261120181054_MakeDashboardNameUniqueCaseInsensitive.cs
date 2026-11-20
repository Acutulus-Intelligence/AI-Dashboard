using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeDashboardNameUniqueCaseInsensitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    dup RECORD;
                    base_name text;
                    candidate text;
                    suffix integer;
                BEGIN
                    FOR dup IN
                        SELECT "Id", "UserId", "Name"
                        FROM (
                            SELECT "Id", "UserId", "Name",
                                   ROW_NUMBER() OVER (
                                       PARTITION BY "UserId", lower("Name")
                                       ORDER BY "CreatedAt", "Id"
                                   ) AS duplicate_rank
                            FROM "Dashboards"
                            WHERE "UserId" IS NOT NULL
                        ) ranked
                        WHERE duplicate_rank > 1
                        ORDER BY "UserId", lower("Name"), duplicate_rank
                    LOOP
                        base_name := left(dup."Name", 190);
                        suffix := 2;
                        LOOP
                            candidate := base_name || ' (' || suffix || ')';
                            EXIT WHEN NOT EXISTS (
                                SELECT 1
                                FROM "Dashboards" d
                                WHERE d."UserId" = dup."UserId"
                                  AND lower(d."Name") = lower(candidate)
                            );
                            suffix := suffix + 1;
                        END LOOP;

                        UPDATE "Dashboards"
                        SET "Name" = candidate,
                            "UpdatedAt" = now()
                        WHERE "Id" = dup."Id";
                    END LOOP;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Dashboards_UserId_Name",
                table: "Dashboards");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_Dashboards_UserId_Name\" " +
                "ON \"Dashboards\" (\"UserId\", lower(\"Name\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX \"IX_Dashboards_UserId_Name\";");

            migrationBuilder.CreateIndex(
                name: "IX_Dashboards_UserId_Name",
                table: "Dashboards",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }
    }
}
