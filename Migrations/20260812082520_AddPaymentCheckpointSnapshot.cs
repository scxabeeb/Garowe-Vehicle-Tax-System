using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentCheckpointSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheckpointId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CheckpointId",
                table: "Payments",
                column: "CheckpointId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Checkpoints_CheckpointId",
                table: "Payments",
                column: "CheckpointId",
                principalTable: "Checkpoints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill: set CheckpointId on existing payments from the
            // collector's current checkpoint so historical collections are
            // correctly attributed before the snapshot column existed.
            migrationBuilder.Sql(
                @"UPDATE `Payments` AS p
                INNER JOIN `Users` AS u ON p.`CollectorId` = u.`Id`
                SET p.`CheckpointId` = u.`CheckpointId`
                WHERE p.`CheckpointId` IS NULL AND u.`CheckpointId` IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Checkpoints_CheckpointId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CheckpointId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CheckpointId",
                table: "Payments");
        }
    }
}
