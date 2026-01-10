using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace VehicleTax.Web.Models
{
    public class Payment
    {
        public int Id { get; set; }

        public int VehicleId { get; set; }
        public Vehicle? Vehicle { get; set; }

        // NEW SYSTEM
        public int MovementId { get; set; }
        public Movement? Movement { get; set; }

        // LEGACY (DO NOT REMOVE)
        public string MovementType { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        // Prefer UTC for consistency
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;

        // Who collected it (make nullable if needed)
        public int CollectorId { get; set; }

        // Receipt reference
        public int? ReceiptReferenceId { get; set; }
        public ReceiptReference? ReceiptReference { get; set; }

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
