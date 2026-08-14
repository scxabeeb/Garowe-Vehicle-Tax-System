namespace VehicleTax.Web.Models;

public class User
{
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public string FullName { get; set; } = "";

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
}