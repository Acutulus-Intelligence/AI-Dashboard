using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnDashboardName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dashboards_UserId",
                table: "Dashboards");

            migrationBuilder.CreateIndex(
                name: "IX_Dashboards_UserId_Name",
                table: "Dashboards",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dashboards_UserId_Name",
                table: "Dashboards");

            migrationBuilder.CreateIndex(
                name: "IX_Dashboards_UserId",
                table: "Dashboards",
                column: "UserId");
        }
    }
}
