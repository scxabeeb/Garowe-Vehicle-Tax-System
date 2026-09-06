namespace VehicleTax.Web.Models
{
    /// <summary>
    /// A single payment included in an RF document.  Each payment can belong to at
    /// most ONE RF document (enforced by a unique index on PaymentId).  The per-payment
    /// FMIS state is derived from the parent RfDocument status..
    /// </summary>
    public class RfPayment
    {
        public int Id { get; set; }

        public int RfDocumentId { get; set; }
        public RfDocument? RfDocument { get; set; }

        public int PaymentId { get; set; }
        public Payment? Payment { get; set; }

        // ── Snapshots taken at inclusion time (never mutated later) ─────
        /// <summary>The payment's audit Reference No. (distinct from the RF number)。</summary>
        public int? ReferenceNo { get; set; }

        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? CollectBy { get; set; }
    }
}