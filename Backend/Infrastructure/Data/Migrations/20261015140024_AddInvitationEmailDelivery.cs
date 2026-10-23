using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationEmailDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyInvites_Token",
                table: "CompanyInvites");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "CompanyInvites");

            // Tokens are now stored as SHA-256 hashes; existing raw-token invites
            // can no longer be validated, so drop them before the unique index
            // (which would otherwise collide on the empty default) is created.
            migrationBuilder.Sql("DELETE FROM \"CompanyInvites\";");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryMethod",
                table: "CompanyInvites",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSentAt",
                table: "CompanyInvites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "CompanyInvites",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvites_TokenHash",
                table: "CompanyInvites",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyInvites_TokenHash",
                table: "CompanyInvites");

            migrationBuilder.DropColumn(
                name: "DeliveryMethod",
                table: "CompanyInvites");

            migrationBuilder.DropColumn(
                name: "LastSentAt",
                table: "CompanyInvites");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "CompanyInvites");

            // The restored Token column defaults to an empty string for every
            // row, which the unique index would reject. Drop the rows first, as
            // Up() does when moving to hashed tokens.
            migrationBuilder.Sql("DELETE FROM \"CompanyInvites\";");

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "CompanyInvites",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvites_Token",
                table: "CompanyInvites",
                column: "Token",
                unique: true);
        }
    }
}
