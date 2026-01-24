public class PaymentDto
{
    public int VehicleId { get; set; }
    public string Movement { get; set; } = "";
    public decimal Amount { get; set; }
    public string ReferenceNumber { get; set; } = "";
    public int CollectorId { get; set; }

    // When true, user confirmed duplicate warning
    public bool Force { get; set; } = false;
}
