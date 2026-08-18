using System;
using System.ComponentModel.DataAnnotations;

namespace VehicleTax.Web.Models
{
    /// <summary>
    /// A transaction imported or manually entered from a Golis statement.
    /// </summary>
    public class GolisTransaction
    {
        public int Id { get; set; }

        public int GolisAuditId { get; set; }
        public GolisAudit? GolisAudit { get; set; }

        [Required]
        public string GolisTransactionReference { get; set; } = string.Empty;

        public DateTime TransactionDate { get; set; }

        public TimeSpan? TransactionTime { get; set; }

        public string? MobileNumber { get; set; }

        public decimal Amount { get; set; }

        public string? Description { get; set; }

        /// <summary>
        /// Golis statement reference number copied from the parent audit.
        /// </summary>
        public string? GolisStatementNumber { get; set; }

        public string? AuditPeriod { get; set; }

        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

        public int? EnteredByUserId { get; set; }
        public User? EnteredByUser { get; set; }

        public string? Notes { get; set; }

        public GolisReconciliationStatus ReconciliationStatus { get; set; } = GolisReconciliationStatus.NeedsReview;

        /// <summary>
        /// Id of the matched system payment, if any.
        /// </summary>
        public int? MatchedPaymentId { get; set; }
        public Payment? MatchedPayment { get; set; }

        /// <summary>
        /// Receipt number of the matched system payment at the time of reconciliation.
        /// </summary>
        public string? MatchedReceiptNumber { get; set; }

        /// <summary>
        /// System amount at the time of reconciliation.
        /// </summary>
        public decimal? MatchedSystemAmount { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public int? ReviewedByUserId { get; set; }
        public User? ReviewedByUser { get; set; }

        /// <summary>
        /// True if this Golis transaction reference appears more than once in the same audit.
        /// </summary>
        public bool IsDuplicate { get; set; }
    }

    public enum GolisReconciliationStatus
    {
        Matched,
        NotInSystem,
        AmountMismatch,
        Duplicate,
        SystemOnly,
        NeedsReview
    }
}
