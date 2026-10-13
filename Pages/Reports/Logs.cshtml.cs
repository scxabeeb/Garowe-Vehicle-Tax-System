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
    public VehicleTax.Web.Services.PaymentSummaryBreakdown Breakdown { get; set; } = new();
    public VehicleTax.Web.Services.Pager Pager { get; set; } = new(0, 1, 10);

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

        Breakdown = await VehicleTax.Web.Services.ReportDetails.SummarizeAsync(query);

        // ---- Summary (whole filtered set, computed in the database) ----
        var totals = await query
            .GroupBy(p => p.IsReverted)
            .Select(g => new { Reverted = g.Key, Count = g.Count(), Amount = g.Sum(p => p.Amount) })
            .ToListAsync();
        CompletedCount = totals.Where(t => !t.Reverted).Sum(t => t.Count);
        RevertedCount = totals.Where(t => t.Reverted).Sum(t => t.Count);
        TotalCompletedAmount = totals.Where(t => !t.Reverted).Sum(t => t.Amount);
        TotalRevertedAmount = totals.Where(t => t.Reverted).Sum(t => t.Amount);
        TotalTransactions = CompletedCount + RevertedCount;

        // ---- Paging ----
        Pager = new VehicleTax.Web.Services.Pager(TotalTransactions, CurrentPage, PageSize);
        CurrentPage = Pager.Page;
        TotalPages = Pager.TotalPages;
        Transactions = await Pager.Apply(query.OrderByDescending(p => p.PaidAt).ThenByDescending(p => p.Id))
            .AsNoTracking()
            .ToListAsync();
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
        sb.AppendLine("System ID,Date,Plate,Car Type,Traffida,Revenue Account,Collector,Checkpoint,Payment Method,Transaction ID,Receipt No,Amount,Status,Cancelled By,Cancel Reason,Remarks,Paid By");

        foreach (var p in data)
        {
            sb.AppendLine(string.Join(",",
                p.Id.ToString(),
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
                p.Amount.ToString("N0"),
                Escape(p.IsReverted ? "Cancelled" : "Completed"),
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
