using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRvFmisModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RvDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RvNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RvDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RevenueAccountId = table.Column<int>(type: "int", nullable: true),
                    PeriodFrom = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PeriodTo = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TotalTransactions = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PreparedById = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FmisStatus = table.Column<int>(type: "int", nullable: false),
                    FmisBatchNumber = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FmisResponse = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TransferredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TransferredById = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CancelledById = table.Column<int>(type: "int", nullable: true),
                    CancellationReason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CancelledResponse = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RvDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RvDocuments_RevenueAccounts_RevenueAccountId",
                        column: x => x.RevenueAccountId,
                        principalTable: "RevenueAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RvDocuments_Users_CancelledById",
                        column: x => x.CancelledById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RvDocuments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RvDocuments_Users_PreparedById",
                        column: x => x.PreparedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RvDocuments_Users_TransferredById",
                        column: x => x.TransferredById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RvDocuments_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RvNumberSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LastRvNumber = table.Column<int>(type: "int", nullable: false),
                    LastAssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RvNumberSequences", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RvAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RvDocumentId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true),
                    Details = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActionAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RvAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RvAuditLogs_RvDocuments_RvDocumentId",
                        column: x => x.RvDocumentId,
                        principalTable: "RvDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RvAuditLogs_Users_ByUserId",
                        column: x => x.ByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RvPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RvDocumentId = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<int>(type: "int", nullable: false),
                    ReferenceNo = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CollectBy = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RvPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RvPayments_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RvPayments_RvDocuments_RvDocumentId",
                        column: x => x.RvDocumentId,
                        principalTable: "RvDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RvAuditLogs_ByUserId",
                table: "RvAuditLogs",
                column: "ByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RvAuditLogs_RvDocumentId",
                table: "RvAuditLogs",
                column: "RvDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_RvDocuments_CancelledById",
                table: "RvDocuments",
                column: "CancelledById");

            migrationBuilder.CreateIndex(
                name: "IX_RvDocuments_CreatedById",
                table: "RvDocuments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_RvDocuments_PreparedById",
                table: "RvDocuments",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_RvDocuments_RevenueAccountId",
                table: "RvDocuments",
                column: "RevenueAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RvDocuments_RvNumber",
                table: "RvDocuments",
                column: "RvNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RvDocuments_TransferredById",
                table: "RvDocuments",
                column: "TransferredById");

            migrationBuilder.CreateIndex(
                name: "IX_RvDocuments_UpdatedById",
                table: "RvDocuments",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_RvPayments_PaymentId",
                table: "RvPayments",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RvPayments_RvDocumentId",
                table: "RvPayments",
                column: "RvDocumentId");

            // Seed the single RF counter row (starts at 0 → first RF is RF-000001)
            migrationBuilder.Sql(
                @"INSERT IGNORE INTO RvNumberSequences (Id, LastRvNumber) VALUES (1, 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RvAuditLogs");

            migrationBuilder.DropTable(
                name: "RvNumberSequences");

            migrationBuilder.DropTable(
                name: "RvPayments");

            migrationBuilder.DropTable(
                name: "RvDocuments");
        }
    }
}
