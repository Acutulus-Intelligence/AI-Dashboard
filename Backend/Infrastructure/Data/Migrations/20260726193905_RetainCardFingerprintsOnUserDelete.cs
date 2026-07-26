using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RetainCardFingerprintsOnUserDelete : Migration
    {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardFingerprints_AspNetUsers_UserId",
                table: "CardFingerprints");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "CardFingerprints",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "EmailHash",
                table: "CardFingerprints",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndDate",
                table: "CardFingerprints",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardFingerprints_EmailHash",
                table: "CardFingerprints",
                column: "EmailHash");

            migrationBuilder.AddForeignKey(
                name: "FK_CardFingerprints_AspNetUsers_UserId",
                table: "CardFingerprints",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CardFingerprints_AspNetUsers_UserId",
                table: "CardFingerprints");

            migrationBuilder.DropIndex(
                name: "IX_CardFingerprints_EmailHash",
                table: "CardFingerprints");

            migrationBuilder.DropColumn(
                name: "EmailHash",
                table: "CardFingerprints");

            migrationBuilder.DropColumn(
                name: "TrialEndDate",
                table: "CardFingerprints");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "CardFingerprints",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CardFingerprints_AspNetUsers_UserId",
                table: "CardFingerprints",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
