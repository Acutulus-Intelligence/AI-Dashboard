using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChartFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "saved_charts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chart_folders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chart_folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chart_folders_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_charts_FolderId",
                table: "saved_charts",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_chart_folders_UserId_Name",
                table: "chart_folders",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_saved_charts_chart_folders_FolderId",
                table: "saved_charts",
                column: "FolderId",
                principalTable: "chart_folders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_saved_charts_chart_folders_FolderId",
                table: "saved_charts");

            migrationBuilder.DropTable(
                name: "chart_folders");

            migrationBuilder.DropIndex(
                name: "IX_saved_charts_FolderId",
                table: "saved_charts");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "saved_charts");
        }
    }
}
