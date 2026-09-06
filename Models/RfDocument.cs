using System.ComponentModel.DataAnnotations;

namespace VehicleTax.Web.Models
{
    /// <summary>
    /// An RF (Revenue/FMIS batch) document — a Finance/Accountant batch containing
    /// one or more successfully paid payments grouped by a financial (revenue) account,
    /// prepared for posting/transfer to the FMIS system.

    /// This is a FINANCE document number and is entirely separate from the per-payment
    /// Payment Reference No. (identifies an individual payment). RF numbers identify batches.
    /// </summary>
    public class RfDocument
    {
        public int Id { get; set; }

        /// <summary>Unique RF number, e.g. RF-000001. Generated ONLY by the backend.</summary>
        [Required]
        [StringLength(50)]
        public string RfNumber { get; set; } = string.Empty;

        /// <summary>Local (system timezone) date the RF cannot be posted.</summary>
        public DateTime RfDate { get; set; } = DateTime.UtcNow;

        // ── Financial account grouping ──────────────────────────────
        public int? RevenueAccountId { get; set; }
        public RevenueAccount? RevenueAccount { get; set; }

        // ── Reporting period the payments belong to ──────────────
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }

        // ── Totals (must exactly equal the sum of the included RfPayments) ──
        public int TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }

        // ── Who prepared it ─────────────────────────────────────────
        public int? PreparedById { get; set; }
        public User? PreparedBy { get; set; }

        // ── Status ───────────────────────────────────────────────────
        public RfStatus Status { get; set; } = RfStatus.Draft;
        public FmisTransferStatus FmisStatus { get; set; } = FmisTransferStatus.NotTransferred;



        // ── FMIS transfer fields ───────────────────────────────────────
        /// <summary>FMIS-returned batch/reference number (kept separate from RfNumber).</summary>
        [StringLength(100)]
        public string? FmisBatchNumber { get; set; }

        /// <summary>Raw FMIS response / error so failed postitions can be reviewedand retried.</summary>
        public string? FmisResponse { get; set; }

        public DateTime? TransferredAt { get; set; }
        public int? TransferredById { get; set; }
        public User? TransferredBy { get; set; }

        // ── Audit trail ───────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedById { get; set; }
        public User? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedById { get; set; }
        public User? UpdatedBy { get; set; }

        // ── Cancellation (preserved, never silently deleted) ──────────
        public DateTime? CancelledAt { get; set; }
        public int? CancelledById { get; set; }
        public User? CancelledBy { get; set; }
        public string? CancellationReason { get; set; }
        public string? CancelledResponse { get; set; }

        // ── Included payments ──────────────────────────────────────
        public ICollection<RfPayment> Payments { get; set; } = new List<RfPayment>();

        // ── Audit log (status changes / FMIS activity) ─────────────
        public ICollection<RfAuditLog> AuditLogs { get; set; } = new List<RfAuditLog>();
    }
}