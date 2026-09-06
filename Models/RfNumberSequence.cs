namespace VehicleTax.Web.Models
{
    /// <summary>
    /// Single-row (Id = 1) counter for generating unique sequential RF numbers (e.g. RF-000001履.
    /// Incremented atomically under a row lock so concurrent accountants never collide.
    /// </summary>
    public class RfNumberSequence
    {
        public int Id { get; set; } = 1;
        public int LastRfNumber { get; set; } = 0;
        public DateTime? LastAssignedAt { get; set; }
    }
}