using System.ComponentModel.DataAnnotations;

namespace VehicleTax.Web.Models;

/// <summary>
/// One entry in the system activity log: a sign-in event, or a record that
/// was created, updated or deleted (written automatically on every save).
/// </summary>
public class ActivityLog
{
    public long Id { get; set; }

    /// <summary>When it happened (UTC).</summary>
    public DateTime At { get; set; } = DateTime.UtcNow;

    /// <summary>Who did it: username, "mobile app" or "system".</summary>
    [StringLength(100)]
    public string Username { get; set; } = "";

    /// <summary>Login, LoginFailed, Logout, Created, Updated or Deleted.</summary>
    [StringLength(20)]
    public string Action { get; set; } = "";

    /// <summary>What kind of record (Payment, Vehicle, User, …).</summary>
    [StringLength(60)]
    public string Entity { get; set; } = "";

    /// <summary>The record's System ID.</summary>
    [StringLength(40)]
    public string? EntityId { get; set; }

    /// <summary>What changed ("Field: old → new; …") or the record's key values.</summary>
    [StringLength(2000)]
    public string? Details { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(200)]
    public string? Path { get; set; }
}

public static class ActivityActions
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string ForcedLogout = "ForcedLogout";

    public static readonly string[] All = { Login, LoginFailed, Logout, Created, Updated, Deleted, ForcedLogout };
}
