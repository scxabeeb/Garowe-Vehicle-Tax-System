using System;
using System.ComponentModel.DataAnnotations;

namespace VehicleTax.Web.Models
{
    /// <summary>
    /// Represents a Golis reconciliation audit session.
    /// </summary>
    public class GolisAudit
    {
        public int Id { get; set; }

        [Required]
        public string StatementNumber { get; set; } = string.Empty;

        public DateTime AuditPeriodFrom { get; set; }
        public DateTime AuditPeriodTo { get; set; }

        public string? UploadedFilePath { get; set; }

        public decimal StatementTotal { get; set; }
        public int StatementTransactionCount { get; set; }

        public int TotalGolisTransactions { get; set; }
        public decimal TotalGolisAmount { get; set; }
        public int TotalSystemTransactions { get; set; }
        public decimal TotalSystemAmount { get; set; }
        public int MatchedCount { get; set; }
        public decimal MatchedAmount { get; set; }
        public int NotInSystemCount { get; set; }
        public decimal NotInSystemAmount { get; set; }
        public int AmountMismatchCount { get; set; }
        public decimal AmountMismatchAmount { get; set; }
        public int DuplicateCount { get; set; }
        public int SystemOnlyCount { get; set; }
        public decimal SystemOnlyAmount { get; set; }
        public decimal Difference { get; set; }

        public string? Notes { get; set; }

        public bool IsFinalized { get; set; }
        public DateTime? FinalizedAt { get; set; }

        public int? FinalizedByUserId { get; set; }
        public User? FinalizedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        public AuditStatus Status { get; set; } = AuditStatus.Draft;

        /// <summary>
        /// Serialized list of Golis transactions for this audit.
        /// </summary>
        public ICollection<GolisTransaction> GolisTransactions { get; set; } = new List<GolisTransaction>();
    }

    public enum AuditStatus
    {
        Draft,
        Finalized,
        Reopened
    }
}
