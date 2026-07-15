using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedChartsAndWidgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Dashboards",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Dashboards",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "My Dashboard");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Dashboards",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Dashboards",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "saved_charts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChartType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    XAxis = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    YAxis = table.Column<string[]>(type: "text[]", nullable: false),
                    Aggregation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GroupBy = table.Column<string>(type: "text", nullable: true),
                    SqlQuery = table.Column<string>(type: "text", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_charts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_charts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dashboard_widgets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DashboardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedChartId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionX = table.Column<int>(type: "integer", nullable: false),
                    PositionY = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_widgets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dashboard_widgets_Dashboards_DashboardId",
                        column: x => x.DashboardId,
                        principalTable: "Dashboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dashboard_widgets_saved_charts_SavedChartId",
                        column: x => x.SavedChartId,
                        principalTable: "saved_charts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dashboards_UserId",
                table: "Dashboards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_widgets_DashboardId",
                table: "dashboard_widgets",
                column: "DashboardId");

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_widgets_SavedChartId",
                table: "dashboard_widgets",
                column: "SavedChartId");

            migrationBuilder.CreateIndex(
                name: "IX_saved_charts_UserId_Title",
                table: "saved_charts",
                columns: new[] { "UserId", "Title" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Dashboards_AspNetUsers_UserId",
                table: "Dashboards",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dashboards_AspNetUsers_UserId",
                table: "Dashboards");

            migrationBuilder.DropTable(
                name: "dashboard_widgets");

            migrationBuilder.DropTable(
                name: "saved_charts");

            migrationBuilder.DropIndex(
                name: "IX_Dashboards_UserId",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Dashboards");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Dashboards");
        }
    }
}
