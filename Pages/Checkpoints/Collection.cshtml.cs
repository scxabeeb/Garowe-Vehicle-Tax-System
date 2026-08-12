using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Checkpoints;

[Authorize(Roles = "Admin")]
public class CollectionModel : PageModel
{
    private readonly AppDbContext _context;

    public CollectionModel(AppDbContext context)
    {
        _context = context;
    }

    // Route parameter
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    // Filters
    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    // Data
    public Checkpoint? Checkpoint { get; set; }
    public List<Payment> Payments { get; set; } = new();

    // Summary
    public int TotalPayments { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalCollectors { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Id = id;

        Checkpoint = await _context.Checkpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (Checkpoint == null)
        {
            TempData["Error"] = "Checkpoint not found.";
            return RedirectToPage("/Checkpoints/Index");
        }

        await LoadCollectionAsync();
        return Page();
    }

    /// <summary>
    /// Collection comes from the Payment.CheckpointId snapshot: payments
    /// collected by users who were assigned to this checkpoint at the time
    /// the payment was recorded.  This ensures that when a collector is
    /// reassigned to a different checkpoint, the previous checkpoint's
    /// collection remains unchanged.
    /// </summary>
    private async Task LoadCollectionAsync()
    {
        // Count collectors currently assigned to this checkpoint (for KPI display)
        TotalCollectors = await _context.Users
            .Where(u => u.CheckpointId == Id)
            .CountAsync();

        var query = _context.Payments
            .Include(p => p.Vehicle)
            .ThenInclude(v => v.CarType)
            .Include(p => p.Movement)
            .Include(p => p.ReceiptReference)
            .Include(p => p.Collector)
            .Include(p => p.Checkpoint)
            .Where(p => p.IsPaid && !p.IsReverted && p.CheckpointId == Id)
            .AsQueryable();

        if (FromDate.HasValue)
        {
            query = query.Where(p => p.PaidAt >= FromDate.Value.Date);
        }

        if (ToDate.HasValue)
        {
            query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));
        }

        Payments = await query
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        TotalPayments = Payments.Count;
        TotalAmount = Payments.Sum(p => p.Amount);
    }

    // =======================
    // Export to CSV
    // =======================
    public async Task<IActionResult> OnGetExportCsvAsync(int id)
    {
        Id = id;
        Checkpoint = await _context.Checkpoints.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (Checkpoint == null)
        {
            return NotFound();
        }

        await LoadCollectionAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Date,Plate,Owner,Mobile,Car Type,Movement,Collector,Checkpoint,Receipt No,Amount");

        foreach (var p in Payments)
        {
            sb.AppendLine(string.Join(",",
                AppTime.ToLocal(p.PaidAt).ToString("yyyy-MM-dd HH:mm"),
                p.Vehicle?.PlateNumber ?? "-",
                p.Vehicle?.OwnerName ?? "-",
                p.Vehicle?.Mobile ?? "-",
                p.Vehicle?.CarType?.Name ?? "-",
                p.Movement?.Name ?? p.MovementType,
                p.Collector?.Username ?? "Unassigned",
                p.Checkpoint?.Name ?? "-",
                p.InvoiceNumber,
                p.Amount.ToString("N2")
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"Checkpoint_{Checkpoint.Name}_{AppTime.Now:yyyyMMddHHmmss}.csv");
    }
}
