using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermissionAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ActionAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ByUserId = table.Column<int>(type: "int", nullable: true),
                    TargetType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Target = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Permission = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OldValue = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NewValue = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Details = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionAuditLogs_Users_ByUserId",
                        column: x => x.ByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Role = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Permission = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserPermissionOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Permission = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Effect = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissionOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissionOverrides_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLogs_ByUserId",
                table: "PermissionAuditLogs",
                column: "ByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLogs_TargetType_Target",
                table: "PermissionAuditLogs",
                columns: new[] { "TargetType", "Target" });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_Role_Permission",
                table: "RolePermissions",
                columns: new[] { "Role", "Permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissionOverrides_UserId_Permission",
                table: "UserPermissionOverrides",
                columns: new[] { "UserId", "Permission" },
                unique: true);

            // ── Seed default role permission sets (idempotent) ────────
            var all = new[]
            {
                "Dashboard.View",
                "Users.View", "Users.Create", "Users.Edit", "Users.Delete",
                "Roles.View", "Roles.Create", "Roles.Edit", "Roles.Delete", "Roles.ManagePermissions",
                "Vehicles.View", "Vehicles.Create", "Vehicles.Edit", "Vehicles.Delete",
                "Payments.View", "Payments.Create", "Payments.Edit", "Payments.Cancel", "Payments.Approve", "Payments.Print",
                "Reports.View", "Reports.Export", "Reports.Print", "Reports.Download",
                "Settings.View", "Settings.Edit",
                "Finance.View", "Finance.Manage"
            };

            var sets = new (string Role, string[] Keys)[]
            {
                ("Admin", all),
                ("Super Admin", all),
                ("Administrator", all),
                ("Manager", new[] { "Dashboard.View", "Users.View", "Roles.View", "Vehicles.View", "Payments.View", "Reports.View", "Settings.View", "Finance.View" }),
                ("Accountant", new[] { "Dashboard.View", "Payments.View", "Payments.Approve", "Reports.View", "Reports.Export", "Reports.Print", "Reports.Download", "Finance.View", "Finance.Manage" }),
                ("Collector", new[] { "Dashboard.View", "Vehicles.View", "Payments.View", "Payments.Create", "Payments.Print" }),
                ("Staff", new[] { "Dashboard.View", "Vehicles.View", "Payments.View", "Payments.Create" }),
                ("Auditor", new[] { "Dashboard.View", "Payments.View", "Reports.View", "Reports.Export", "Reports.Print", "Reports.Download", "Finance.View" })
            };

            var seed = new System.Text.StringBuilder();
            foreach (var (roleName, keys) in sets)
                foreach (var key in keys)
                    seed.Append("INSERT IGNORE INTO `RolePermissions` (`Role`, `Permission`) VALUES ('")
                        .Append(roleName).Append("', '").Append(key).AppendLine("');");

            migrationBuilder.Sql(seed.ToString());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionAuditLogs");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserPermissionOverrides");
        }
    }
}
