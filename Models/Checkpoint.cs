namespace VehicleTax.Web.Models;

public class Checkpoint
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Reverse navigation: users (collectors) assigned to this checkpoint.
    public ICollection<User> Users { get; set; } = new List<User>();
}
