using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeCatalogFieldsToPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeMonthlyPriceId",
                table: "SubscriptionPlans",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeProductId",
                table: "SubscriptionPlans",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeYearlyPriceId",
                table: "SubscriptionPlans",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripeMonthlyPriceId",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "StripeProductId",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "StripeYearlyPriceId",
                table: "SubscriptionPlans");
        }
    }
}
