using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTextAlignmentToWidgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TextHorizontalAlign",
                table: "dashboard_widgets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TextVerticalAlign",
                table: "dashboard_widgets",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextHorizontalAlign",
                table: "dashboard_widgets");

            migrationBuilder.DropColumn(
                name: "TextVerticalAlign",
                table: "dashboard_widgets");
        }
    }
}
