namespace VehicleTax.Web.Models
{
    /// <summary>
    /// Tracks the last assigned audit Reference Number for payments.
    /// </summary>
    /// A single row (Id = 1) holds the running counter that is incremented
    /// atomically (under row-lock) every time a payment is successfully recorded as Paid.
    /// This guarantees sequential, unique, never-reused audit reference numbers
    /// even under concurrent payment recording by multiple collectors.
    /// </summary>
    public class PaymentReferenceSequence
    {
        public int Id { get; set; } = 1;
        public int LastReferenceNo { get; set; } = 0;
        public DateTime? LastAssignedAt { get; set; }
    }
}