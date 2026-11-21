using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CascadeWidgetDeleteOnChartDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dashboard_widgets_saved_charts_SavedChartId",
                table: "dashboard_widgets");

            migrationBuilder.AddForeignKey(
                name: "FK_dashboard_widgets_saved_charts_SavedChartId",
                table: "dashboard_widgets",
                column: "SavedChartId",
                principalTable: "saved_charts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dashboard_widgets_saved_charts_SavedChartId",
                table: "dashboard_widgets");

            migrationBuilder.AddForeignKey(
                name: "FK_dashboard_widgets_saved_charts_SavedChartId",
                table: "dashboard_widgets",
                column: "SavedChartId",
                principalTable: "saved_charts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
