using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMistakeBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MistakeByUserId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_MistakeByUserId",
                table: "Payments",
                column: "MistakeByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Users_MistakeByUserId",
                table: "Payments",
                column: "MistakeByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Users_MistakeByUserId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_MistakeByUserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MistakeByUserId",
                table: "Payments");
        }
    }
}
