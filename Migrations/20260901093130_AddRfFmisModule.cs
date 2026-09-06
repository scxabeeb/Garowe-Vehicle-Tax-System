using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddRfFmisModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RfDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RfNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RfDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
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
                    table.PrimaryKey("PK_RfDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfDocuments_RevenueAccounts_RevenueAccountId",
                        column: x => x.RevenueAccountId,
                        principalTable: "RevenueAccounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RfDocuments_Users_CancelledById",
                        column: x => x.CancelledById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RfDocuments_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RfDocuments_Users_PreparedById",
                        column: x => x.PreparedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RfDocuments_Users_TransferredById",
                        column: x => x.TransferredById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RfDocuments_Users_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RfNumberSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    LastRfNumber = table.Column<int>(type: "int", nullable: false),
                    LastAssignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RfNumberSequences", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RfAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RfDocumentId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RfAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfAuditLogs_RfDocuments_RfDocumentId",
                        column: x => x.RfDocumentId,
                        principalTable: "RfDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RfAuditLogs_Users_ByUserId",
                        column: x => x.ByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RfPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RfDocumentId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RfPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RfPayments_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RfPayments_RfDocuments_RfDocumentId",
                        column: x => x.RfDocumentId,
                        principalTable: "RfDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RfAuditLogs_ByUserId",
                table: "RfAuditLogs",
                column: "ByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RfAuditLogs_RfDocumentId",
                table: "RfAuditLogs",
                column: "RfDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_RfDocuments_CancelledById",
                table: "RfDocuments",
                column: "CancelledById");

            migrationBuilder.CreateIndex(
                name: "IX_RfDocuments_CreatedById",
                table: "RfDocuments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_RfDocuments_PreparedById",
                table: "RfDocuments",
                column: "PreparedById");

            migrationBuilder.CreateIndex(
                name: "IX_RfDocuments_RevenueAccountId",
                table: "RfDocuments",
                column: "RevenueAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RfDocuments_RfNumber",
                table: "RfDocuments",
                column: "RfNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RfDocuments_TransferredById",
                table: "RfDocuments",
                column: "TransferredById");

            migrationBuilder.CreateIndex(
                name: "IX_RfDocuments_UpdatedById",
                table: "RfDocuments",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_RfPayments_PaymentId",
                table: "RfPayments",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RfPayments_RfDocumentId",
                table: "RfPayments",
                column: "RfDocumentId");

            // Seed the single RF counter row (starts at 0 → first RF is RF-000001)
            migrationBuilder.Sql(
                @"INSERT IGNORE INTO RfNumberSequences (Id, LastRfNumber) VALUES (1, 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RfAuditLogs");

            migrationBuilder.DropTable(
                name: "RfNumberSequences");

            migrationBuilder.DropTable(
                name: "RfPayments");

            migrationBuilder.DropTable(
                name: "RfDocuments");
        }
    }
}
