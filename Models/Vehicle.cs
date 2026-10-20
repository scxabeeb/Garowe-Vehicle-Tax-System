namespace VehicleTax.Web.Models;

public class Vehicle
{
    public int Id { get; set; }

    public string PlateNumber { get; set; } = "";

    public string OwnerName { get; set; } = "";

    public string Mobile { get; set; } = ""; // 📱 Owner mobile number

    public int CarTypeId { get; set; }

    public CarType? CarType { get; set; }

    /// <summary>Inactive vehicles cannot get new invoices or payments (existing invoices can still be paid).</summary>
    public bool IsActive { get; set; } = true;

    public const string InactiveMessage = "This vehicle is inactive and cannot be charged. Ask an admin to set it Active.";
}
