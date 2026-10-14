using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTariffIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Movements",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true); // every existing tariff stays active
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Movements");
        }
    }
}
