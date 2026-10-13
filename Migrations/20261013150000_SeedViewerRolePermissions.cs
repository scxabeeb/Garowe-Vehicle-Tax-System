using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VehicleTax.Web.Data;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <summary>
    /// Pages now enforce permissions, so the Viewer role (which had no
    /// defaults) gets read-only access: Dashboard and Reports. Only applied
    /// when Viewer has no permissions yet, so existing settings are kept.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261013150000_SeedViewerRolePermissions")]
    public partial class SeedViewerRolePermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO `RolePermissions` (`Role`, `Permission`)
SELECT x.r, x.p FROM (
    SELECT 'Viewer' AS r, 'Dashboard.View' AS p
    UNION ALL SELECT 'Viewer', 'Reports.View'
) AS x
WHERE NOT EXISTS (SELECT 1 FROM (SELECT `Role` FROM `RolePermissions` WHERE `Role` = 'Viewer') AS v);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
