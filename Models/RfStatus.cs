namespace VehicleTax.Web.Models
{
    /// <summary>
    /// Lifecycle of an RF (Revenue/FMIS) document.  An RF is a Finance/Accountant
    /// batch document that groups one or more successfully paid payments for transfer to FMIS.
    /// It is completely distinct from the Payment Reference No. (which identifies an individual payment).
    /// </summary>
    public enum RfStatus
    {
        Draft,
        Prepared,
        ReadyForFmis,
        Transferred,
        Cancelled
    }

    /// <summary>
    /// FMIS transfer state of an RF document./per-payment.
    /// </summary>
    public enum FmisTransferStatus
    {
        NotTransferred,
        ReadyForFmis,
        Failed,
        Transferred
    }
}