using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreserveAcceptedInvitesAndTrackEmailSends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyInvites_CompanyRoles_RoleId",
                table: "CompanyInvites");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "CompanyInvites",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "RoleName",
                table: "CompanyInvites",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Backfill the snapshot so existing accepted invites keep showing
            // their role name even after that role is deleted.
            migrationBuilder.Sql(
                "UPDATE \"CompanyInvites\" AS ci " +
                "SET \"RoleName\" = cr.\"Name\" " +
                "FROM \"CompanyRoles\" AS cr " +
                "WHERE ci.\"RoleId\" = cr.\"Id\";");

            migrationBuilder.CreateTable(
                name: "CompanyInviteEmailSends",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyInviteEmailSends", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyInviteEmailSends_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyInviteEmailSends_CompanyId_SentAt",
                table: "CompanyInviteEmailSends",
                columns: new[] { "CompanyId", "SentAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyInvites_CompanyRoles_RoleId",
                table: "CompanyInvites",
                column: "RoleId",
                principalTable: "CompanyRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CompanyInvites_CompanyRoles_RoleId",
                table: "CompanyInvites");

            migrationBuilder.DropTable(
                name: "CompanyInviteEmailSends");

            migrationBuilder.DropColumn(
                name: "RoleName",
                table: "CompanyInvites");

            // RoleId was nullable only to preserve accepted invites whose role
            // had been deleted; those rows cannot satisfy the old Restrict FK.
            migrationBuilder.Sql("DELETE FROM \"CompanyInvites\" WHERE \"RoleId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "CompanyInvites",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyInvites_CompanyRoles_RoleId",
                table: "CompanyInvites",
                column: "RoleId",
                principalTable: "CompanyRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
