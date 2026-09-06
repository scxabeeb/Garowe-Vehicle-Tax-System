using System;
using System.ComponentModel.DataAnnotations.Schema;
using VehicleTax.Web;

namespace VehicleTax.Web.Models
{
    public class Payment
    {
        public int Id { get; set; }

        public int VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }

        public int MovementId { get; set; }
        public Movement? Movement { get; set; }

        // LEGACY
        public string MovementType { get; set; } = string.Empty;

        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;
        public bool IsPaid { get; set; } = false;

        // Collector (your Users table)
        public int? CollectorId { get; set; }
        public User? Collector { get; set; }

        /// <summary>
        /// Snapshot of the checkpoint the collector was assigned to at the
        /// time the payment was recorded.  This ensures historical
        /// payments stay attributed to the original checkpoint even when
        /// the collector is later reassigned to a different checkpoint.
        /// </summary>
        public int? CheckpointId { get; set; }
        public Checkpoint? Checkpoint { get; set; }

                public int? ReceiptReferenceId { get; set; }
        public ReceiptReference? ReceiptReference { get; set; }

        // ──────────────────────────────────────────────────────────
        // Audit Reference Number
        // ──────────────────────────────────────────────────────────
        /// <summary>
        /// Audit Reference No. — assigned only when the payment is successfully
        /// recorded (becomes Paid). NULL for pending / failed / cancelled-before-payment.
        /// Once assigned, the number is permanent and never reused, even if the
        /// payment is later cancelled, so the auditor can trace the full history.
        /// </summary>
        public int? ReferenceNo { get; set; }

        // ──────────────────────────────────────────────────────────
        // Golis / Mobile-Money integration fields
        // ──────────────────────────────────────────────────────────
        /// <summary>
        /// The payer's phone number as reported by Golis (billInfo.paidBy).
        /// </summary>
        public string? PaidBy { get; set; }

        /// <summary>
        /// The Golis transaction ID (transacionInfo.tansactionId).
        /// </summary>
        public string? TransactionId { get; set; }

        /// <summary>
        /// Free-form remarks from the Golis notification (billInfo.remarks).
        /// </summary>
        public string? Remarks { get; set; }

        /// <summary>
        /// Payment method / channel code as reported by Golis (billInfo.paidAt).
        /// e.g. "MMT" = Mobile Money Transfer.
        /// </summary>
        public string? PaymentMethod { get; set; }

        // ──────────────────────────────────────────────────────────
        // Computed invoice number — NOT mapped to a DB column.
        // Format: YYYYMM + 6-digit serial  (e.g. 202607000155)
        // This matches the invoice-id format used by the Golis API.
        // ──────────────────────────────────────────────────────────
        [NotMapped]
        public string InvoiceNumber => $"{AppTime.ToLocal(PaidAt):yyyyMM}{Id:D6}";

        [NotMapped]
        public string ShortCode => InvoiceNumber.Length > 9 ? InvoiceNumber.Substring(0, 9) : InvoiceNumber;

        // 🔴 Revert system
        public bool IsReverted { get; set; } = false;
        public string? RevertReason { get; set; }
        public DateTime? RevertedAt { get; set; }

        public int? RevertedByUserId { get; set; }
        public User? RevertedByUser { get; set; }   // 🔥 THIS WAS MISSING

        // Backward compatibility
        [NotMapped]
        public decimal AmountPaid
        {
            get => Amount;
            set => Amount = value;
        }

        [NotMapped]
        public DateTime PaymentDate
        {
            get => PaidAt;
            set => PaidAt = value;
        }
    }
}
