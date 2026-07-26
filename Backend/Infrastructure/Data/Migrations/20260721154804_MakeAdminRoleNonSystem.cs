using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeAdminRoleNonSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"UPDATE ""CompanyRoles""
                  SET ""IsSystemRole"" = false
                  WHERE ""Name"" = 'Admin' AND ""IsSystemRole"" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"UPDATE ""CompanyRoles""
                  SET ""IsSystemRole"" = true
                  WHERE ""Name"" = 'Admin' AND ""IsSystemRole"" = false");
        }
    }
}
