using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_data_collections_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_data_collections_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.DropForeignKey(
                name: "FK_saved_datasets_AspNetUsers_UserId",
                table: "saved_datasets");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "saved_datasets",
                newName: "CollectionId");

            migrationBuilder.RenameIndex(
                name: "IX_saved_datasets_UserId_Name",
                table: "saved_datasets",
                newName: "IX_saved_datasets_CollectionId_Name");

            migrationBuilder.AddColumn<string>(
                name: "DataModel",
                table: "saved_charts",
                type: "jsonb",
                nullable: true);

            // Backfill: one "My uploads" collection per user who had personal
            // datasets, then point their datasets at it. The per-company unique
            // name is guaranteed by suffixing with the user id.
            migrationBuilder.Sql(@"
INSERT INTO ""data_collections"" (""Id"", ""Name"", ""CompanyId"", ""CreatedById"", ""CreatedAt"")
SELECT
    gen_random_uuid(),
    'My uploads (' || left(u.""Id""::text, 8) || ')',
    u.""CompanyId"",
    u.""Id"",
    now()
FROM ""AspNetUsers"" u
WHERE EXISTS (SELECT 1 FROM ""saved_datasets"" sd WHERE sd.""CollectionId"" = u.""Id"");
");

            migrationBuilder.Sql(@"
UPDATE ""saved_datasets"" sd
SET ""CollectionId"" = dc.""Id""
FROM ""data_collections"" dc
WHERE sd.""CollectionId"" = dc.""CreatedById"";
");

            migrationBuilder.CreateIndex(
                name: "IX_data_collections_CompanyId_Name",
                table: "data_collections",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_data_collections_CreatedById_Name",
                table: "data_collections",
                columns: new[] { "CreatedById", "Name" },
                unique: true,
                filter: "\"CompanyId\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_saved_datasets_data_collections_CollectionId",
                table: "saved_datasets",
                column: "CollectionId",
                principalTable: "data_collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_saved_datasets_data_collections_CollectionId",
                table: "saved_datasets");

            migrationBuilder.DropTable(
                name: "data_collections");

            migrationBuilder.DropColumn(
                name: "DataModel",
                table: "saved_charts");

            migrationBuilder.RenameColumn(
                name: "CollectionId",
                table: "saved_datasets",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_saved_datasets_CollectionId_Name",
                table: "saved_datasets",
                newName: "IX_saved_datasets_UserId_Name");

            migrationBuilder.AddForeignKey(
                name: "FK_saved_datasets_AspNetUsers_UserId",
                table: "saved_datasets",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}