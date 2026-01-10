namespace VehicleTax.Web.Models;

public class Vehicle
{
    public int Id { get; set; }

    public string PlateNumber { get; set; } = "";

    public string OwnerName { get; set; } = "";

    public string Mobile { get; set; } = ""; // 📱 Owner mobile number

    public int CarTypeId { get; set; }

    public CarType? CarType { get; set; }
}
