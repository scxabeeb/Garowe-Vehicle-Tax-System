using System.ComponentModel.DataAnnotations;

namespace VehicleTax.Web.Models;

/// <summary>
/// A payment moved from one Traffida (and so revenue account) to another,
/// because it was collected under the wrong one. The amount never changes.
/// </summary>
public class PaymentTransfer
{
    public int Id { get; set; }

    public int PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;

    public int FromMovementId { get; set; }
    [StringLength(100)] public string FromMovement { get; set; } = "";
    [StringLength(150)] public string? FromAccount { get; set; }

    public int ToMovementId { get; set; }
    [StringLength(100)] public string ToMovement { get; set; } = "";
    [StringLength(150)] public string? ToAccount { get; set; }

    public decimal Amount { get; set; }

    [StringLength(500)] public string Reason { get; set; } = "";

    public int? ByUserId { get; set; }
    [StringLength(100)] public string ByUsername { get; set; } = "";
}
