public class ReceiptReference
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = "";
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedBy { get; set; }   // NEW
}
