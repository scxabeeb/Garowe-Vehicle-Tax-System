using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace VehicleTax.Web.Models;

public class ReceiptReference
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = "";

    // Status
    public bool IsUsed { get; set; }
    public bool IsCancelled { get; set; }

    // Used info
    public string? UsedBy { get; set; }
    public DateTime? UsedAt { get; set; }

    public int? VehicleId { get; set; }

    [ForeignKey(nameof(VehicleId))]
    public Vehicle? Vehicle { get; set; }

    // Cancel info (only when Available -> Cancelled)
    public string? CancelledReason { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
}
