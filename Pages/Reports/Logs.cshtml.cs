using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

public class LogsModel : PageModel
{
    private readonly AppDbContext _context;

    public LogsModel(AppDbContext context)
    {
        _context = context;
    }

    // Filters
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public string? Action { get; set; }   // All | Completed | Reverted
    [BindProperty(SupportsGet = true)] public int? CollectorId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CheckpointId { get; set; }
    [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 10;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    // Data
    public List<Payment> Transactions { get; set; } = new();
    public SelectList Collectors { get; set; } = null!;
    public SelectList Checkpoints { get; set; } = null!;
    public SelectList Movements { get; set; } = null!;

    // Summary
    public int TotalTransactions { get; set; }
    public decimal TotalCompletedAmount { get; set; }
    public decimal TotalRevertedAmount { get; set; }
    public int CompletedCount { get; set; }
    public int RevertedCount { get; set; }
    public int TotalPages { get; set; }

    public async Task OnGetAsync()
    {
        Collectors = new SelectList(
            await _context.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync(),
            "Id", "Username"
        );

        Checkpoints = new SelectList(
            await _context.Checkpoints.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name"
        );

        Movements = new SelectList(
            await _context.Movements.AsNoTracking().OrderBy(m => m.Name).ToListAsync(),
            "Id", "Name"
        );

        var query = _context.Payments
            .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
            .Include(p => p.Movement).ThenInclude(m => m.RevenueAccount)
            .Include(p => p.ReceiptReference)
            .Include(p => p.Collector)
            .Include(p => p.Checkpoint)
            .Include(p => p.RevertedByUser)
            .AsQueryable();

        // ---- Filters ----
        if (FromDate.HasValue)
            query = query.Where(p => p.PaidAt >= FromDate.Value.Date);

        if (ToDate.HasValue)
            query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

        if (CollectorId.HasValue)
            query = query.Where(p => p.CollectorId == CollectorId.Value);

        // Filter by the Payment.CheckpointId snapshot (not the collector's
        // current checkpoint) so reassignment does not change historical data.
        if (CheckpointId.HasValue)
            query = query.Where(p => p.CheckpointId == CheckpointId.Value);

        if (MovementId.HasValue)
            query = query.Where(p => p.MovementId == MovementId.Value);

        if (!string.IsNullOrEmpty(Action) && Action != "All")
        {
            if (Action == "Completed")
                query = query.Where(p => p.IsPaid && !p.IsReverted);

            if (Action == "Reverted")
                query = query.Where(p => p.IsReverted);
        }

        // ---- Summary (computed before paging) ----
        var allMatching = query.AsEnumerable();

        TotalTransactions = allMatching.Count();
        CompletedCount = allMatching.Count(p => !p.IsReverted);
        RevertedCount = allMatching.Count(p => p.IsReverted);
        TotalCompletedAmount = allMatching.Where(p => !p.IsReverted).Sum(p => (decimal?)p.Amount) ?? 0m;
        TotalRevertedAmount = allMatching.Where(p => p.IsReverted).Sum(p => (decimal?)p.Amount) ?? 0m;

        // ---- Paging ----
        if (PageSize > 0)
        {
            TotalPages = (int)Math.Ceiling(TotalTransactions / (double)PageSize);
            Transactions = await query
                .OrderByDescending(p => p.PaidAt)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
        else
        {
            TotalPages = 1;
            Transactions = await query.OrderByDescending(p => p.PaidAt).ToListAsync();
        }
    }

    // =======================
    // EXPORT TO CSV
    // =======================
    public async Task<IActionResult> OnGetExportCsvAsync()
    {
        var query = _context.Payments
            .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
            .Include(p => p.Movement).ThenInclude(m => m.RevenueAccount)
            .Include(p => p.ReceiptReference)
            .Include(p => p.Collector)
            .Include(p => p.Checkpoint)
            .Include(p => p.RevertedByUser)
            .AsQueryable();

        // Same filters as OnGetAsync
        if (FromDate.HasValue)
            query = query.Where(p => p.PaidAt >= FromDate.Value.Date);

        if (ToDate.HasValue)
            query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

        if (CollectorId.HasValue)
            query = query.Where(p => p.CollectorId == CollectorId.Value);

        if (CheckpointId.HasValue)
            query = query.Where(p => p.CheckpointId == CheckpointId.Value);

        if (MovementId.HasValue)
            query = query.Where(p => p.MovementId == MovementId.Value);

        if (!string.IsNullOrEmpty(Action) && Action != "All")
        {
            if (Action == "Completed")
                query = query.Where(p => p.IsPaid && !p.IsReverted);

            if (Action == "Reverted")
                query = query.Where(p => p.IsReverted);
        }

        var data = await query.OrderByDescending(p => p.PaidAt).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Date,Plate,Car Type,Movement,Revenue Account,Collector,Checkpoint,Payment Method,Transaction ID,Receipt No,Amount,Status,Reverted By,Revert Reason,Remarks,Paid By");

        foreach (var p in data)
        {
            sb.AppendLine(string.Join(",",
                Escape(AppTime.ToLocal(p.PaidAt).ToString("dd MMM yyyy HH:mm")),
                Escape(p.Vehicle?.PlateNumber ?? "-"),
                Escape(p.Vehicle?.CarType?.Name ?? "-"),
                Escape(p.Movement?.Name ?? p.MovementType),
                Escape(p.Movement?.RevenueAccount != null
                    ? $"{p.Movement.RevenueAccount.AccountCode} - {p.Movement.RevenueAccount.AccountName}"
                    : "-"),
                Escape(p.Collector?.Username ?? "Unassigned"),
                Escape(p.Checkpoint?.Name ?? "Unassigned"),
                Escape(p.PaymentMethod ?? "-"),
                Escape(p.TransactionId ?? "-"),
                Escape(p.ReceiptReference?.ReferenceNumber ?? "-"),
                p.Amount.ToString("N2"),
                Escape(p.IsReverted ? "Reverted" : "Completed"),
                Escape(p.RevertedByUser?.Username ?? "-"),
                Escape(p.RevertReason),
                Escape(p.Remarks),
                Escape(p.PaidBy ?? "-")
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"Logs_{AppTime.Now:yyyyMMddHHmmss}.csv");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
