using System.ComponentModel.DataAnnotations.Schema;

namespace VehicleTax.Web.Models;

public class TaxAmount
{
    public int Id { get; set; }

    public int CarTypeId { get; set; }
    public CarType? CarType { get; set; }

    public int MovementId { get; set; }
    public Movement? Movement { get; set; }

    public decimal Amount { get; set; }

    // ✅ TEMP COMPATIBILITY (so old code still compiles)
    [NotMapped]
    public string? MovementType => Movement?.Name;
}
