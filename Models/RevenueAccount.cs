using System.ComponentModel.DataAnnotations;

namespace VehicleTax.Web.Models
{
    public class RevenueAccount
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string AccountCode { get; set; } = "";

        [Required]
        [StringLength(100)]
        public string AccountName { get; set; } = "";

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Movement>? Movements { get; set; }

        // ✅ TEMP COMPATIBILITY
        [Display(Name = "Display Name")]
        public string DisplayName => $"{AccountCode} - {AccountName}";
    }
}
