using System;
using System.ComponentModel.DataAnnotations.Schema;

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

        // Collector (your Users table)
        public int? CollectorId { get; set; }
        public User? Collector { get; set; }

        public int? ReceiptReferenceId { get; set; }
        public ReceiptReference? ReceiptReference { get; set; }

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
