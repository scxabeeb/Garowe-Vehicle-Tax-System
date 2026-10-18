using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Transactions;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    // Filters
    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Action { get; set; }   // All | Completed | Reverted

    // Page size selector (default = 10)
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public Pager Pager { get; set; } = new(0, 1, 10);

    public List<User> Users { get; set; } = new();
    public List<Payment> Transactions { get; set; } = new();

    // Summary totals
    public int TotalTransactions { get; set; }
    public decimal TotalAmount { get; set; }
    public int CompletedCount { get; set; }
    public decimal CompletedAmount { get; set; }
    public int CancelledCount { get; set; }
    public decimal CancelledAmount { get; set; }
    // Money actually collected (cancelled invoices were never collected).
    public decimal NetAmount => CompletedAmount;
    public int PendingCount { get; set; }
    public decimal PendingAmount { get; set; }

    public async Task OnGetAsync()
    {
        Users = await _context.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync();

        var query = Filtered();

        // Summary totals over the whole filtered set, computed in the database.
        var totals = await query
            // 0 = completed (paid), 1 = pending (not paid yet), 2 = cancelled
            .GroupBy(p => p.IsReverted ? 2 : p.IsPaid ? 0 : 1)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(p => p.Amount) })
            .ToListAsync();
        CompletedCount = totals.Where(t => t.Status == 0).Sum(t => t.Count);
        CompletedAmount = totals.Where(t => t.Status == 0).Sum(t => t.Amount);
        PendingCount = totals.Where(t => t.Status == 1).Sum(t => t.Count);
        PendingAmount = totals.Where(t => t.Status == 1).Sum(t => t.Amount);
        CancelledCount = totals.Where(t => t.Status == 2).Sum(t => t.Count);
        CancelledAmount = totals.Where(t => t.Status == 2).Sum(t => t.Amount);
        TotalTransactions = CompletedCount + PendingCount + CancelledCount;
        TotalAmount = CompletedAmount + PendingAmount + CancelledAmount;

        Pager = new Pager(TotalTransactions, CurrentPage, PageSize);

        // Latest action first: use RevertedAt for cancelled, PaidAt for completed.
        Transactions = await Pager.Apply(query
                .OrderByDescending(p => p.IsReverted ? p.RevertedAt : p.PaidAt)
                .ThenByDescending(p => p.Id))
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>Payments matching the filters, with what the table shows.</summary>
    private IQueryable<Payment> Filtered()
    {
        var query = _context.Payments
            .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
            .Include(p => p.ReceiptReference)
            .Include(p => p.RevertedByUser)
            .Include(p => p.Collector)
            .AsQueryable();

        // Filters (use action date: PaidAt for completed, RevertedAt for cancelled)
        if (FromDate.HasValue)
            query = query.Where(p =>
                (!p.IsReverted && p.PaidAt >= AppTime.ToUtc(FromDate.Value.Date)) ||
                (p.IsReverted && p.RevertedAt.HasValue && p.RevertedAt.Value >= AppTime.ToUtc(FromDate.Value.Date)));

        if (ToDate.HasValue)
            query = query.Where(p =>
                (!p.IsReverted && p.PaidAt < AppTime.ToUtc(ToDate.Value.Date.AddDays(1))) ||
                (p.IsReverted && p.RevertedAt.HasValue && p.RevertedAt.Value < AppTime.ToUtc(ToDate.Value.Date.AddDays(1))));

        if (UserId.HasValue)
            query = query.Where(p =>
                p.CollectorId == UserId.Value ||
                p.RevertedByUserId == UserId.Value);

        if (!string.IsNullOrEmpty(Action) && Action != "All")
        {
            if (Action == "Completed")
                query = query.Where(p => p.IsPaid && !p.IsReverted);

            if (Action == "Pending")
                query = query.Where(p => !p.IsPaid && !p.IsReverted);

            if (Action == "Reverted")
                query = query.Where(p => p.IsReverted);
        }

        return query;
    }

    // EXPORT ONLY FILTERED DATA (ignores paging)
    public IActionResult OnGetExportFiltered()
    {
        var query = Filtered();

        var data = query
            .OrderByDescending(p => p.IsReverted ? p.RevertedAt : p.PaidAt)
            .ThenByDescending(p => p.Id)
            .AsNoTracking()
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("System ID,Ref No,Date,Plate,Car Type,Movement,Collector,Amount,Receipt No,Golis Bill No,Status,Cancelled By,Cancel Reason");

        foreach (var t in data)
        {
            sb.AppendLine(string.Join(",",
                t.Id,
                Escape(t.ReferenceNo?.ToString() ?? "-"),
                Escape(AppTime.ToLocal(t.PaidAt).ToString("dd MMM yyyy HH:mm")),
                Escape(t.Vehicle?.PlateNumber),
                Escape(t.Vehicle?.CarType?.Name),
                Escape(t.MovementType),
                Escape(t.Collector?.Username ?? "System"),
                VehicleTax.Web.Services.Money.Csv(t.Amount),
                Escape(t.ReceiptReference != null ? t.ReceiptReference.ReferenceNumber : t.InvoiceNumber),
                Escape(string.IsNullOrWhiteSpace(t.TransactionId) ? "-" : t.TransactionId),
                Escape(t.IsReverted ? "Cancelled" : "Completed"),
                Escape(t.RevertedByUser?.Username),
                Escape(t.RevertReason)
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"Transactions_{AppTime.Now:yyyyMMdd_HHmm}.csv");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
