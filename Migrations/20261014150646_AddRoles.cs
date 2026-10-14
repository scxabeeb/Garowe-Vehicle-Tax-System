using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            // Standard roles, then every role already used by a user or a permission grant.
            migrationBuilder.Sql(@"
INSERT IGNORE INTO Roles (Name, Description, IsActive, CreatedAt, CreatedBy) VALUES
 ('Admin', 'Full access to everything, including users and permissions.', 1, UTC_TIMESTAMP(), 'system'),
 ('Collector', 'Collects tax: invoices, receipts, vehicles and their own dashboard.', 1, UTC_TIMESTAMP(), 'system'),
 ('Viewer', 'Read-only: dashboard and reports.', 1, UTC_TIMESTAMP(), 'system'),
 ('Auditor', 'Reviews payments, reports and finance.', 1, UTC_TIMESTAMP(), 'system'),
 ('Accountant', 'Finance, RV and report exports.', 1, UTC_TIMESTAMP(), 'system'),
 ('Manager', 'Oversees users, vehicles, payments and reports.', 1, UTC_TIMESTAMP(), 'system');");
            migrationBuilder.Sql(@"
INSERT IGNORE INTO Roles (Name, Description, IsActive, CreatedAt, CreatedBy)
SELECT DISTINCT TRIM(Role), 'Custom role.', 1, UTC_TIMESTAMP(), 'system' FROM Users WHERE TRIM(IFNULL(Role, '')) <> '';");
            migrationBuilder.Sql(@"
INSERT IGNORE INTO Roles (Name, Description, IsActive, CreatedAt, CreatedBy)
SELECT DISTINCT TRIM(Role), 'Custom role.', 1, UTC_TIMESTAMP(), 'system' FROM RolePermissions WHERE TRIM(IFNULL(Role, '')) <> '';");

            // Creating and deleting roles is now a real permission: make sure Admin has it.
            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (Role, Permission)
SELECT 'Admin', k.Permission FROM
 (SELECT 'Roles.View' AS Permission UNION ALL SELECT 'Roles.Create' UNION ALL SELECT 'Roles.Edit' UNION ALL SELECT 'Roles.Delete') k
WHERE NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.Role = 'Admin' AND rp.Permission = k.Permission);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Roles");
        }
    }
}
