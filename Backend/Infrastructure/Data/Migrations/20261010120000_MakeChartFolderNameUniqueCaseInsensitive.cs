using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeChartFolderNameUniqueCaseInsensitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chart_folders_UserId_Name",
                table: "chart_folders");

            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"IX_chart_folders_UserId_Name\" " +
                "ON \"chart_folders\" (\"UserId\", lower(\"Name\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX \"IX_chart_folders_UserId_Name\";");

            migrationBuilder.CreateIndex(
                name: "IX_chart_folders_UserId_Name",
                table: "chart_folders",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }
    }
}
