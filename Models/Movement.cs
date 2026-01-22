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
    }
}
