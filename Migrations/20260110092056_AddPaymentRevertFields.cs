using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRevertFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReverted",
                table: "Payments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RevertReason",
                table: "Payments",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RevertedAt",
                table: "Payments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevertedByUserId",
                table: "Payments",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReverted",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RevertReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RevertedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RevertedByUserId",
                table: "Payments");
        }
    }
}
