namespace VehicleTax.Web.Models;

/// <summary>A permission granted to everyone holding a given role.</summary>
public class RolePermission
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(50)]
    public string Role { get; set; } = "";

    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string Permission { get; set; } = "";
}
