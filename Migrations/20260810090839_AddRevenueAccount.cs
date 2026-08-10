using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRevenueAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RevenueAccountId",
                table: "Movements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RevenueAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AccountCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccountName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueAccounts", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_RevenueAccountId",
                table: "Movements",
                column: "RevenueAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueAccounts_AccountCode",
                table: "RevenueAccounts",
                column: "AccountCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Movements_RevenueAccounts_RevenueAccountId",
                table: "Movements",
                column: "RevenueAccountId",
                principalTable: "RevenueAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Movements_RevenueAccounts_RevenueAccountId",
                table: "Movements");

            migrationBuilder.DropTable(
                name: "RevenueAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Movements_RevenueAccountId",
                table: "Movements");

            migrationBuilder.DropColumn(
                name: "RevenueAccountId",
                table: "Movements");
        }
    }
}
