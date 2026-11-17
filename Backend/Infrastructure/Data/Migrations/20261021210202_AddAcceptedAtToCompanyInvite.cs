using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAcceptedAtToCompanyInvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyInvites_CompanyId_Email",
                table: "CompanyInvites");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "CompanyInvites",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvites_CompanyId_Email",
                table: "CompanyInvites",
                columns: new[] { "CompanyId", "Email" },
                unique: true,
                filter: "\"IsAccepted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompanyInvites_CompanyId_Email",
                table: "CompanyInvites");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "CompanyInvites");

            // The restored index is no longer filtered, so consumed rows kept for
            // traceability would collide with a pending invite for the same email.
            migrationBuilder.Sql("DELETE FROM \"CompanyInvites\" WHERE \"IsAccepted\" = true;");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInvites_CompanyId_Email",
                table: "CompanyInvites",
                columns: new[] { "CompanyId", "Email" },
                unique: true);
        }
    }
}
