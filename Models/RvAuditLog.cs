namespace VehicleTax.Web.Models
{
    /// <summary>
    /// Immutable audit trail entry for every RF action — creation, status changes,
    /// FMIS transfer attempts (success or failure —, cancellation etc，。 so the auditor can answer:
    /// Who created the RF? When? Which payments were included? What was the total? Was it
    /// transferred to FMIS? When/By whom? What FMIS reference/batch number was returned?
    /// </summary>
    public class RvAuditLog
    {
        public int Id { get; set; }

        public int RvDocumentId { get; set; }
        public RvDocument? RvDocument { get; set; }

        /// <summary>Human-readable action, e.g. "RF Created", "Prepared", "FMIS Transfer Attempted", "FMIS Transferred", "FMIS Failed", "Cancelled".</summary>
        public string Action { get; set; } = string.Empty;

        public RvStatus? FromStatus { get; set; }
        public RvStatus? ToStatus { get; set; }

        public string? Details { get; set; }

        public DateTime ActionAt { get; set; } = DateTime.UtcNow;
        public int? ByUserId { get; set; }
        public User? ByUser { get; set; }
    }
}