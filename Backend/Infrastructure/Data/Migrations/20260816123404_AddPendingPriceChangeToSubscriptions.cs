using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingPriceChangeToSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NextPrice",
                table: "UserSubscriptions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextPriceEffectiveDate",
                table: "UserSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NextPrice",
                table: "CompanySubscriptions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextPriceEffectiveDate",
                table: "CompanySubscriptions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextPrice",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "NextPriceEffectiveDate",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "NextPrice",
                table: "CompanySubscriptions");

            migrationBuilder.DropColumn(
                name: "NextPriceEffectiveDate",
                table: "CompanySubscriptions");
        }
    }
}
