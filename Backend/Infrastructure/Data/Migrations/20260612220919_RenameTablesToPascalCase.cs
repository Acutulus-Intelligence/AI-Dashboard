using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameTablesToPascalCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_companies_CompanyId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_company_roles_CompanyRoleId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_companies_AspNetUsers_OwnerId",
                table: "companies");

            migrationBuilder.DropForeignKey(
                name: "FK_company_invites_companies_CompanyId",
                table: "company_invites");

            migrationBuilder.DropForeignKey(
                name: "FK_company_invites_company_roles_RoleId",
                table: "company_invites");

            migrationBuilder.DropForeignKey(
                name: "FK_company_roles_companies_CompanyId",
                table: "company_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_company_subscriptions_companies_CompanyId",
                table: "company_subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_company_subscriptions_subscription_plans_PlanId",
                table: "company_subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_dashboards_companies_CompanyId",
                table: "dashboards");

            migrationBuilder.DropForeignKey(
                name: "FK_DashboardUser_dashboards_DashboardsId",
                table: "DashboardUser");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshToken_AspNetUsers_UserId",
                table: "RefreshToken");

            migrationBuilder.DropForeignKey(
                name: "FK_user_subscriptions_AspNetUsers_UserId",
                table: "user_subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_user_subscriptions_subscription_plans_PlanId",
                table: "user_subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_dashboards",
                table: "dashboards");

            migrationBuilder.DropPrimaryKey(
                name: "PK_companies",
                table: "companies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user_subscriptions",
                table: "user_subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_user_subscriptions_UserId",
                table: "user_subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_subscription_plans",
                table: "subscription_plans");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RefreshToken",
                table: "RefreshToken");

            migrationBuilder.DropPrimaryKey(
                name: "PK_company_subscriptions",
                table: "company_subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_company_subscriptions_CompanyId",
                table: "company_subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_company_roles",
                table: "company_roles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_company_invites",
                table: "company_invites");

            migrationBuilder.RenameTable(
                name: "dashboards",
                newName: "Dashboards");

            migrationBuilder.RenameTable(
                name: "companies",
                newName: "Companies");

            migrationBuilder.RenameTable(
                name: "user_subscriptions",
                newName: "UserSubscriptions");

            migrationBuilder.RenameTable(
                name: "subscription_plans",
                newName: "SubscriptionPlans");

            migrationBuilder.RenameTable(
                name: "RefreshToken",
                newName: "RefreshTokens");

            migrationBuilder.RenameTable(
                name: "company_subscriptions",
                newName: "CompanySubscriptions");

            migrationBuilder.RenameTable(
                name: "company_roles",
                newName: "CompanyRoles");

            migrationBuilder.RenameTable(
                name: "company_invites",
                newName: "CompanyInvites");

            migrationBuilder.RenameIndex(
                name: "IX_dashboards_CompanyId",
                table: "Dashboards",
                newName: "IX_Dashboards_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_companies_OwnerId",
                table: "Companies",
                newName: "IX_Companies_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_user_subscriptions_PlanId",
                table: "UserSubscriptions",
                newName: "IX_UserSubscriptions_PlanId");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshToken_UserId",
                table: "RefreshTokens",
                newName: "IX_RefreshTokens_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshToken_Token",
                table: "RefreshTokens",
                newName: "IX_RefreshTokens_Token");

            migrationBuilder.RenameIndex(
                name: "IX_company_subscriptions_PlanId",
                table: "CompanySubscriptions",
                newName: "IX_CompanySubscriptions_PlanId");

            migrationBuilder.RenameIndex(
                name: "IX_company_roles_CompanyId",
                table: "CompanyRoles",
                newName: "IX_CompanyRoles_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_company_invites_Token",
                table: "CompanyInvites",
                newName: "IX_CompanyInvites_Token");

            migrationBuilder.RenameIndex(
                name: "IX_company_invites_RoleId",
                table: "CompanyInvites",
                newName: "IX_CompanyInvites_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_company_invites_CompanyId",
                table: "CompanyInvites",
                newName: "IX_CompanyInvites_CompanyId");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Companies",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CompanyInvites",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Dashboards",
                table: "Dashboards",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Companies",
                table: "Companies",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserSubscriptions",
                table: "UserSubscriptions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SubscriptionPlans",
                table: "SubscriptionPlans",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RefreshTokens",
                table: "RefreshTokens",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanySubscriptions",
                table: "CompanySubscriptions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyRoles",
                table: "CompanyRoles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CompanyInvites",
                table: "CompanyInvites",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_UserId",
                table: "UserSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptions_CompanyId",
                table: "CompanySubscriptions",
                column: "CompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Companies_CompanyId",
                table: "AspNetUsers",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_CompanyRoles_CompanyRoleId",
                table: "AspNetUsers",
                column: "CompanyRoleId",
                principalTable: "CompanyRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_AspNetUsers_OwnerId",
                table: "Companies",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyInvites_Companies_CompanyId",
                table: "CompanyInvites",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyInvites_CompanyRoles_RoleId",
                table: "CompanyInvites",
                column: "RoleId",
                principalTable: "CompanyRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyRoles_Companies_CompanyId",
                table: "CompanyRoles",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanySubscriptions_Companies_CompanyId",
                table: "CompanySubscriptions",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanySubscriptions_SubscriptionPlans_PlanId",
                table: "CompanySubscriptions",
                column: "PlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Dashboards_Companies_CompanyId",
                table: "Dashboards",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DashboardUser_Dashboards_DashboardsId",
                table: "DashboardUser",
                column: "DashboardsId",
                principalTable: "Dashboards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_AspNetUsers_UserId",
                table: "RefreshTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_AspNetUsers_UserId",
                table: "UserSubscriptions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPlans_PlanId",
                table: "UserSubscriptions",
                column: "PlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Companies_CompanyId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_CompanyRoles_CompanyRoleId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_AspNetUsers_OwnerId",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyInvites_Companies_CompanyId",
                table: "CompanyInvites");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyInvites_CompanyRoles_RoleId",
                table: "CompanyInvites");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyRoles_Companies_CompanyId",
                table: "CompanyRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanySubscriptions_Companies_CompanyId",
                table: "CompanySubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanySubscriptions_SubscriptionPlans_PlanId",
                table: "CompanySubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Dashboards_Companies_CompanyId",
                table: "Dashboards");

            migrationBuilder.DropForeignKey(
                name: "FK_DashboardUser_Dashboards_DashboardsId",
                table: "DashboardUser");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_AspNetUsers_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_AspNetUsers_UserId",
                table: "UserSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPlans_PlanId",
                table: "UserSubscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Dashboards",
                table: "Dashboards");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Companies",
                table: "Companies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserSubscriptions",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_UserId",
                table: "UserSubscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SubscriptionPlans",
                table: "SubscriptionPlans");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RefreshTokens",
                table: "RefreshTokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanySubscriptions",
                table: "CompanySubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_CompanySubscriptions_CompanyId",
                table: "CompanySubscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyRoles",
                table: "CompanyRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CompanyInvites",
                table: "CompanyInvites");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CompanyInvites");

            migrationBuilder.RenameTable(
                name: "Dashboards",
                newName: "dashboards");

            migrationBuilder.RenameTable(
                name: "Companies",
                newName: "companies");

            migrationBuilder.RenameTable(
                name: "UserSubscriptions",
                newName: "user_subscriptions");

            migrationBuilder.RenameTable(
                name: "SubscriptionPlans",
                newName: "subscription_plans");

            migrationBuilder.RenameTable(
                name: "RefreshTokens",
                newName: "RefreshToken");

            migrationBuilder.RenameTable(
                name: "CompanySubscriptions",
                newName: "company_subscriptions");

            migrationBuilder.RenameTable(
                name: "CompanyRoles",
                newName: "company_roles");

            migrationBuilder.RenameTable(
                name: "CompanyInvites",
                newName: "company_invites");

            migrationBuilder.RenameIndex(
                name: "IX_Dashboards_CompanyId",
                table: "dashboards",
                newName: "IX_dashboards_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_Companies_OwnerId",
                table: "companies",
                newName: "IX_companies_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_UserSubscriptions_PlanId",
                table: "user_subscriptions",
                newName: "IX_user_subscriptions_PlanId");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshToken",
                newName: "IX_RefreshToken_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshToken",
                newName: "IX_RefreshToken_Token");

            migrationBuilder.RenameIndex(
                name: "IX_CompanySubscriptions_PlanId",
                table: "company_subscriptions",
                newName: "IX_company_subscriptions_PlanId");

            migrationBuilder.RenameIndex(
                name: "IX_CompanyRoles_CompanyId",
                table: "company_roles",
                newName: "IX_company_roles_CompanyId");

            migrationBuilder.RenameIndex(
                name: "IX_CompanyInvites_Token",
                table: "company_invites",
                newName: "IX_company_invites_Token");

            migrationBuilder.RenameIndex(
                name: "IX_CompanyInvites_RoleId",
                table: "company_invites",
                newName: "IX_company_invites_RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_CompanyInvites_CompanyId",
                table: "company_invites",
                newName: "IX_company_invites_CompanyId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_dashboards",
                table: "dashboards",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_companies",
                table: "companies",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user_subscriptions",
                table: "user_subscriptions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_subscription_plans",
                table: "subscription_plans",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RefreshToken",
                table: "RefreshToken",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_company_subscriptions",
                table: "company_subscriptions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_company_roles",
                table: "company_roles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_company_invites",
                table: "company_invites",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_UserId",
                table: "user_subscriptions",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_subscriptions_CompanyId",
                table: "company_subscriptions",
                column: "CompanyId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_companies_CompanyId",
                table: "AspNetUsers",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_company_roles_CompanyRoleId",
                table: "AspNetUsers",
                column: "CompanyRoleId",
                principalTable: "company_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_companies_AspNetUsers_OwnerId",
                table: "companies",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_company_invites_companies_CompanyId",
                table: "company_invites",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_invites_company_roles_RoleId",
                table: "company_invites",
                column: "RoleId",
                principalTable: "company_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_company_roles_companies_CompanyId",
                table: "company_roles",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_subscriptions_companies_CompanyId",
                table: "company_subscriptions",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_company_subscriptions_subscription_plans_PlanId",
                table: "company_subscriptions",
                column: "PlanId",
                principalTable: "subscription_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_dashboards_companies_CompanyId",
                table: "dashboards",
                column: "CompanyId",
                principalTable: "companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DashboardUser_dashboards_DashboardsId",
                table: "DashboardUser",
                column: "DashboardsId",
                principalTable: "dashboards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshToken_AspNetUsers_UserId",
                table: "RefreshToken",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_subscriptions_AspNetUsers_UserId",
                table: "user_subscriptions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_subscriptions_subscription_plans_PlanId",
                table: "user_subscriptions",
                column: "PlanId",
                principalTable: "subscription_plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
