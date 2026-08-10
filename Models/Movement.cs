using System.ComponentModel.DataAnnotations;

namespace VehicleTax.Web.Models
{
    public class Movement
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = "";

        [Required]
        public int? CarTypeId { get; set; }   // 🔑 MUST be nullable

        public CarType? CarType { get; set; }

        // 🔑 Revenue Account link (nullable to preserve existing data on migration)
        public int? RevenueAccountId { get; set; }

        public RevenueAccount? RevenueAccount { get; set; }
    }
}
