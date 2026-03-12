namespace VehicleTax.Web.Models;

public class CarType
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    // Once true, editing and deleting are forbidden
    public bool IsLocked { get; set; }
}
