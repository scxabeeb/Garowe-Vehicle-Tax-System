using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReferenceNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReferenceNo",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentReferenceSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LastReferenceNo = table.Column<int>(type: "int", nullable: false),
                    LastAssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReferenceSequences", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

                        migrationBuilder.CreateIndex(
                name: "IX_Payments_ReferenceNo",
                table: "Payments",
                column: "ReferenceNo",
                unique: true);

            // Seed the single sequence row so the counter starts at 0
            migrationBuilder.Sql(
                @"INSERT IGNORE INTO PaymentReferenceSequences (Id, LastReferenceNo) VALUES (1, 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentReferenceSequences");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReferenceNo",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReferenceNo",
                table: "Payments");
        }
    }
}
