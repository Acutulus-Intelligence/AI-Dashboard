using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_external_connections_UserId_Name",
                table: "external_connections");

            migrationBuilder.AddColumn<List<Guid>>(
                name: "AllowedRoleIds",
                table: "external_connections",
                type: "uuid[]",
                nullable: false,
                defaultValue: Array.Empty<Guid>());

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "external_connections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "external_connections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Private");

            migrationBuilder.AddColumn<bool>(
                name: "CanManageConnections",
                table: "CompanyRoles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_external_connections_CompanyId_Name",
                table: "external_connections",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL AND \"Visibility\" <> 'Private'");

            migrationBuilder.CreateIndex(
                name: "IX_external_connections_UserId_Name",
                table: "external_connections",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"CompanyId\" IS NULL OR \"Visibility\" = 'Private'");

            migrationBuilder.AddForeignKey(
                name: "FK_external_connections_Companies_CompanyId",
                table: "external_connections",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_external_connections_Companies_CompanyId",
                table: "external_connections");

            migrationBuilder.DropIndex(
                name: "IX_external_connections_CompanyId_Name",
                table: "external_connections");

            migrationBuilder.DropIndex(
                name: "IX_external_connections_UserId_Name",
                table: "external_connections");

            migrationBuilder.DropColumn(
                name: "AllowedRoleIds",
                table: "external_connections");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "external_connections");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "external_connections");

            migrationBuilder.DropColumn(
                name: "CanManageConnections",
                table: "CompanyRoles");

            migrationBuilder.CreateIndex(
                name: "IX_external_connections_UserId_Name",
                table: "external_connections",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }
    }
}
