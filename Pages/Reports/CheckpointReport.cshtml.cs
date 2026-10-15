using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

public class CheckpointReportModel : PageModel
{
    private readonly AppDbContext _context;

    public CheckpointReportModel(AppDbContext context)
    {
        _context = context;
    }

    // Filters
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public int? CheckpointId { get; set; }
    [BindProperty(SupportsGet = true)] public string? GolisBillNo { get; set; }
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 10;
    public VehicleTax.Web.Services.Pager Pager => new(TotalPayments, CurrentPage, PageSize, "CurrentPage");
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    // Helpers
    public List<CheckpointSummaryRow> CheckpointSummaries { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public SelectList CheckpointList { get; set; } = null!;

    // Totals
    public int TotalPayments { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalCheckpoints { get; set; }
    public int TotalPages { get; set; }

    // Chart data (JSON strings for Chart.js)
    public string CheckpointChartLabels { get; set; } = "[]";
    public string CheckpointChartAmounts { get; set; } = "[]";

    public class CheckpointSummaryRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int TotalPayments { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public VehicleTax.Web.Services.PaymentSummaryBreakdown Breakdown { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (PageSize <= 0) PageSize = VehicleTax.Web.Services.Pager.AllRows; // "All"
        CheckpointList = new SelectList(
            await _context.Checkpoints.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name"
        );

        // ---- Checkpoint-level summary (always all checkpoints) ----
        var summaryQuery = _context.Payments
            .Include(p => p.Checkpoint)
            .Include(p => p.Collector)
                .ThenInclude(c => c!.Checkpoint)
            .Where(p => p.IsPaid && !p.IsReverted)
            .AsQueryable();

        if (FromDate.HasValue)
            summaryQuery = summaryQuery.Where(p => p.PaidAt >= FromDate.Value.Date);

        if (ToDate.HasValue)
            summaryQuery = summaryQuery.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

        // Only registered checkpoints are listed (all of them, even with no
        // payments); payments with no checkpoint are not shown as a group.
        // Attribution: the payment's checkpoint snapshot, else the collector's.
        var totalsByCheckpoint = summaryQuery
            .AsEnumerable()
            .Select(p => new { CheckpointId = p.CheckpointId ?? p.Collector?.CheckpointId, p.Amount })
            .Where(x => x.CheckpointId.HasValue)
            .GroupBy(x => x.CheckpointId!.Value)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Amount: g.Sum(x => x.Amount)));

        var checkpointSummaries = (await _context.Checkpoints.AsNoTracking().ToListAsync())
            .Select(c => new CheckpointSummaryRow
            {
                Id = c.Id,
                Name = c.Name,
                TotalPayments = totalsByCheckpoint.TryGetValue(c.Id, out var t) ? t.Count : 0,
                TotalAmount = totalsByCheckpoint.TryGetValue(c.Id, out var t2) ? t2.Amount : 0m
            })
            .OrderByDescending(x => x.TotalAmount)
            .ThenBy(x => x.Name)
            .ToList();

        CheckpointSummaries = checkpointSummaries;

        TotalCheckpoints = CheckpointSummaries.Count;

        CheckpointChartLabels = System.Text.Json.JsonSerializer.Serialize(
            CheckpointSummaries.Select(c => c.Name));
        CheckpointChartAmounts = System.Text.Json.JsonSerializer.Serialize(
            CheckpointSummaries.Select(c => c.TotalAmount));

        // ---- Detailed payment list ----
        var query = _context.Payments
            .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
            .Include(p => p.Movement).ThenInclude(m => m.RevenueAccount)
            .Include(p => p.ReceiptReference)
            .Include(p => p.Collector)
                .ThenInclude(c => c!.Checkpoint)
            .Include(p => p.Checkpoint)
            .Where(p => p.IsPaid && !p.IsReverted)
            .AsQueryable();

        if (FromDate.HasValue)
            query = query.Where(p => p.PaidAt >= FromDate.Value.Date);

        if (ToDate.HasValue)
            query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

        // Filter by the Payment.CheckpointId snapshot first; if null, fall
        // back to the collector's currently-assigned checkpoint so old
        // payments without a snapshot are still attributed.
        if (CheckpointId.HasValue)
            query = query.Where(p => p.CheckpointId == CheckpointId.Value ||
                (p.CheckpointId == null && p.Collector != null && p.Collector.CheckpointId == CheckpointId.Value));

        if (!string.IsNullOrWhiteSpace(GolisBillNo))
            query = query.Where(p => p.TransactionId != null && p.TransactionId.Contains(GolisBillNo));

        TotalPayments = await query.CountAsync();
        TotalAmount = await query.SumAsync(p => (decimal?)p.Amount) ?? 0m;
        Breakdown = await VehicleTax.Web.Services.ReportDetails.SummarizeAsync(query);

        if (PageSize > 0)
        {
            TotalPages = (int)Math.Ceiling(TotalPayments / (double)PageSize);
            Payments = await query
                .OrderByDescending(p => p.PaidAt)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
        else
        {
            TotalPages = 1;
            Payments = await query.OrderByDescending(p => p.PaidAt).ToListAsync();
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
            .Where(p => p.IsPaid && !p.IsReverted)
            .AsQueryable();

        if (FromDate.HasValue)
            query = query.Where(p => p.PaidAt >= FromDate.Value.Date);

        if (ToDate.HasValue)
            query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

        if (CheckpointId.HasValue)
            query = query.Where(p => p.CheckpointId == CheckpointId.Value);

        if (!string.IsNullOrWhiteSpace(GolisBillNo))
            query = query.Where(p => p.TransactionId != null && p.TransactionId.Contains(GolisBillNo));

        var data = await query.OrderByDescending(p => p.PaidAt).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("System ID,Ref No,Date,Plate,Owner,Mobile,Car Type,Tariff,Revenue Account,Collector,Checkpoint,Payment Method,Receipt No,Golis Bill No,Amount");

        foreach (var p in data)
        {
            sb.AppendLine(string.Join(",",
                p.Id.ToString(),
                p.ReferenceNo?.ToString() ?? "-",
                Escape(AppTime.ToLocal(p.PaidAt).ToString("yyyy-MM-dd HH:mm")),
                Escape(p.Vehicle?.PlateNumber ?? "-"),
                Escape(p.Vehicle?.OwnerName ?? "-"),
                Escape(p.Vehicle?.Mobile ?? "-"),
                Escape(p.Vehicle?.CarType?.Name ?? "-"),
                Escape(p.Movement?.Name ?? p.MovementType),
                Escape(p.Movement?.RevenueAccount != null
                    ? $"{p.Movement.RevenueAccount.AccountCode} - {p.Movement.RevenueAccount.AccountName}"
                    : "-"),
                Escape(p.Collector?.Username ?? "Unassigned"),
                Escape(p.Checkpoint?.Name ?? "Unassigned"),
                Escape(p.PaymentMethod ?? "-"),
                Escape(p.InvoiceNumber),
                Escape(p.TransactionId ?? "-"),
                p.Amount.ToString("N0")
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"CheckpointReport_{AppTime.Now:yyyyMMddHHmmss}.csv");
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
