using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDatasets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DatasetId",
                table: "saved_charts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "saved_datasets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ColumnNames = table.Column<string[]>(type: "text[]", nullable: false),
                    ColumnTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    RowsJson = table.Column<string>(type: "text", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_datasets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_datasets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_datasets_UserId_Name",
                table: "saved_datasets",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_datasets");

            migrationBuilder.DropColumn(
                name: "DatasetId",
                table: "saved_charts");
        }
    }
}
