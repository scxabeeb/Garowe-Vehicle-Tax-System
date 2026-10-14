using System.ComponentModel.DataAnnotations;

namespace VehicleTax.Web.Models;

/// <summary>
/// A user role (Admin, Collector, …). Users store the role's name; its
/// permissions are the RolePermissions rows with the same name.
/// </summary>
public class Role
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = "";

    [StringLength(250)]
    public string? Description { get; set; }

    /// <summary>Inactive roles cannot be given to users.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    /// <summary>Admin and Super Admin cannot be renamed, deactivated or deleted.</summary>
    public static bool IsProtected(string? name) =>
        string.Equals(name, "Admin", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Super Admin", StringComparison.OrdinalIgnoreCase);
}
