using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDataCollectionVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_data_collections_CompanyId_Name",
                table: "data_collections");

            migrationBuilder.DropIndex(
                name: "IX_data_collections_CreatedById_Name",
                table: "data_collections");

            migrationBuilder.AddColumn<List<Guid>>(
                name: "AllowedRoleIds",
                table: "data_collections",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'::uuid[]");

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "data_collections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Private");

            migrationBuilder.CreateIndex(
                name: "IX_data_collections_CompanyId_Name",
                table: "data_collections",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL AND \"Visibility\" <> 'Private'");

            migrationBuilder.CreateIndex(
                name: "IX_data_collections_CreatedById_Name",
                table: "data_collections",
                columns: new[] { "CreatedById", "Name" },
                unique: true,
                filter: "\"CompanyId\" IS NULL OR \"Visibility\" = 'Private'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_data_collections_CompanyId_Name",
                table: "data_collections");

            migrationBuilder.DropIndex(
                name: "IX_data_collections_CreatedById_Name",
                table: "data_collections");

            migrationBuilder.DropColumn(
                name: "AllowedRoleIds",
                table: "data_collections");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "data_collections");

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
        }
    }
}
