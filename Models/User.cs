using System.ComponentModel.DataAnnotations.Schema;

namespace VehicleTax.Web.Models;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public string FullName { get; set; } = "";

    // Full name when set, otherwise the username.
    [NotMapped]
    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Username : FullName;

    // "Full Name (@username)", so users who share a name can be told apart.
    [NotMapped]
    public string NameWithUsername => string.IsNullOrWhiteSpace(FullName) ? Username : $"{FullName} (@{Username})";

    public string Phone { get; set; } = "";

    public string Password { get; set; } = "";

    public string Role { get; set; } = "Collector";

    // Stored as: vehicle.create,vehicle.edit,payment.create
    public string Permissions { get; set; } = "";

    // Collector workstation/location assignment.
    public int? CheckpointId { get; set; }
    public Checkpoint? Checkpoint { get; set; }

    // New: lock / unlock support
    public bool IsLocked { get; set; } = false;

    /// <summary>
    /// Stamped into every web login and app token ("sv" claim). Raising it
    /// signs the user out everywhere: older logins no longer match.
    /// </summary>
    public int SessionVersion { get; set; }
}

public static class SessionClaims
{
    public const string Version = "sv";

    /// <summary>The session number a login carries; logins from before it existed carry 0.</summary>
    public static int Of(System.Security.Claims.ClaimsPrincipal? principal) =>
        int.TryParse(principal?.FindFirst(Version)?.Value, out var v) ? v : 0;
}