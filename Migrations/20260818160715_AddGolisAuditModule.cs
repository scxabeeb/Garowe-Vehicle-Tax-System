using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VehicleTax.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddGolisAuditModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GolisAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StatementNumber = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuditPeriodFrom = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AuditPeriodTo = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UploadedFilePath = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StatementTotal = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    StatementTransactionCount = table.Column<int>(type: "int", nullable: false),
                    TotalGolisTransactions = table.Column<int>(type: "int", nullable: false),
                    TotalGolisAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalSystemTransactions = table.Column<int>(type: "int", nullable: false),
                    TotalSystemAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    MatchedCount = table.Column<int>(type: "int", nullable: false),
                    MatchedAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NotInSystemCount = table.Column<int>(type: "int", nullable: false),
                    NotInSystemAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    AmountMismatchCount = table.Column<int>(type: "int", nullable: false),
                    AmountMismatchAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DuplicateCount = table.Column<int>(type: "int", nullable: false),
                    SystemOnlyCount = table.Column<int>(type: "int", nullable: false),
                    SystemOnlyAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Difference = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsFinalized = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinalizedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GolisAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GolisAudits_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GolisAudits_Users_FinalizedByUserId",
                        column: x => x.FinalizedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GolisTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    GolisAuditId = table.Column<int>(type: "int", nullable: false),
                    GolisTransactionReference = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TransactionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TransactionTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    MobileNumber = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GolisStatementNumber = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuditPeriod = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImportedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EnteredByUserId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReconciliationStatus = table.Column<int>(type: "int", nullable: false),
                    MatchedPaymentId = table.Column<int>(type: "int", nullable: true),
                    MatchedReceiptNumber = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MatchedSystemAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    IsDuplicate = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GolisTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GolisTransactions_GolisAudits_GolisAuditId",
                        column: x => x.GolisAuditId,
                        principalTable: "GolisAudits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GolisTransactions_Payments_MatchedPaymentId",
                        column: x => x.MatchedPaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GolisTransactions_Users_EnteredByUserId",
                        column: x => x.EnteredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GolisTransactions_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GolisAudits_CreatedByUserId",
                table: "GolisAudits",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GolisAudits_FinalizedByUserId",
                table: "GolisAudits",
                column: "FinalizedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GolisTransactions_EnteredByUserId",
                table: "GolisTransactions",
                column: "EnteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GolisTransactions_GolisAuditId_ReconciliationStatus",
                table: "GolisTransactions",
                columns: new[] { "GolisAuditId", "ReconciliationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_GolisTransactions_GolisTransactionReference",
                table: "GolisTransactions",
                column: "GolisTransactionReference");

            migrationBuilder.CreateIndex(
                name: "IX_GolisTransactions_MatchedPaymentId",
                table: "GolisTransactions",
                column: "MatchedPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_GolisTransactions_ReviewedByUserId",
                table: "GolisTransactions",
                column: "ReviewedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GolisTransactions");

            migrationBuilder.DropTable(
                name: "GolisAudits");
        }
    }
}
