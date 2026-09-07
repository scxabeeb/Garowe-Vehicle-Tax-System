namespace VehicleTax.Web.Models;

/// <summary>
/// Immutable audit entry for every permission change: who changed what, for
/// which role or user, previous value → new value, and when.
/// </summary>
public class PermissionAuditLog
{
    public int Id { get; set; }

    public DateTime ActionAt { get; set; } = DateTime.UtcNow;

    public int? ByUserId { get; set; }
    public User? ByUser { get; set; }

    /// <summary>"Role" or "User".</summary>
    [System.ComponentModel.DataAnnotations.StringLength(20)]
    public string TargetType { get; set; } = "Role";

    /// <summary>Role name or affected user's username.</summary>
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    public string Target { get; set; } = "";

    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string Permission { get; set; } = "";

    /// <summary>"Granted" / "Revoked" / "Inherited" / "-" when not previously set.</summary>
    [System.ComponentModel.DataAnnotations.StringLength(20)]
    public string OldValue { get; set; } = "";

    [System.ComponentModel.DataAnnotations.StringLength(20)]
    public string NewValue { get; set; } = "";

    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string? Details { get; set; }
}
