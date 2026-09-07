namespace VehicleTax.Web.Models;

/// <summary>
/// A per-user permission override. Allow grants the permission even when the
/// user's role does not include it; Deny revokes it even when the role grants it
/// (Deny always wins). Absence of an override means "inherit from role".
/// </summary>
public class UserPermissionOverride
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string Permission { get; set; } = "";

    /// <summary>Allow = explicit grant, Deny = explicit revoke.</summary>
    public PermissionEffect Effect { get; set; } = PermissionEffect.Allow;
}

public enum PermissionEffect
{
    Allow = 1,
    Deny = 2
}
