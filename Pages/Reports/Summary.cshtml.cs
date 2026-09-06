using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

public class SummaryModel : PageModel
{
    private readonly AppDbContext _context;

    public SummaryModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public int? CollectorId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CheckpointId { get; set; }
    [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }

    public SelectList Collectors { get; set; } = null!;
    public SelectList Checkpoints { get; set; } = null!;
    public SelectList Movements { get; set; } = null!;

    // Summary totals
    public int TotalTransactions { get; set; }
    public int CompletedCount { get; set; }
    public decimal CompletedAmount { get; set; }
    public int CancelledCount { get; set; }
    public decimal CancelledAmount { get; set; }
    public decimal NetAmount => CompletedAmount - CancelledAmount;

    // Breakdowns
    public List<CollectorSummary> CollectorSummaries { get; set; } = new();
    public List<MovementSummary> MovementSummaries { get; set; } = new();
    public List<CheckpointSummary> CheckpointSummaries { get; set; } = new();
    public List<StatusSummary> StatusSummaries { get; set; } = new();

    // Per-payment detail records (include the audit Reference No.)
    public List<PaymentRecord> PaymentRecords { get; set; } = new();

    public class PaymentRecord
    {
        public int? ReferenceNo { get; set; }
        public int Id { get; set; }
        public string InvoiceId { get; set; } = string.Empty;
        public string PlateNumber { get; set; } = string.Empty;
        public string MovementType { get; set; } = string.Empty;
        public string CollectorName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaidAt { get; set; }
        public bool IsReverted { get; set; }
    }

    public class CollectorSummary
    {
        public string CollectorName { get; set; } = "";
        public int Completed { get; set; }
        public decimal CompletedAmount { get; set; }
        public int Cancelled { get; set; }
        public decimal CancelledAmount { get; set; }
    }

    public class MovementSummary
    {
        public string MovementName { get; set; } = "";
        public int Completed { get; set; }
        public decimal CompletedAmount { get; set; }
        public int Cancelled { get; set; }
        public decimal CancelledAmount { get; set; }
    }

    public class CheckpointSummary
    {
        public string CheckpointName { get; set; } = "";
        public int Completed { get; set; }
        public decimal CompletedAmount { get; set; }
        public int Cancelled { get; set; }
        public decimal CancelledAmount { get; set; }
    }

    public class StatusSummary
    {
        public string Status { get; set; } = "";
        public int Count { get; set; }
        public decimal Amount { get; set; }
    }

    public void OnGet()
    {
        LoadDropdowns();
        ComputeSummaries();
    }

    private void LoadDropdowns()
    {
        Collectors = new SelectList(
            _context.Users.AsNoTracking().OrderBy(u => u.Username).ToList(),
            "Id", "Username");

        Checkpoints = new SelectList(
            _context.Checkpoints.AsNoTracking().OrderBy(c => c.Name).ToList(),
            "Id", "Name");

        Movements = new SelectList(
            _context.Movements.AsNoTracking().OrderBy(m => m.Name).ToList(),
            "Id", "Name");
    }

    private IQueryable<Payment> GetFilteredQuery()
    {
        var query = _context.Payments
            .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
            .Include(p => p.Movement)
            .Include(p => p.Collector)
            .Include(p => p.Checkpoint)
            .AsQueryable();

        if (FromDate.HasValue)
            query = query.Where(p =>
                (!p.IsReverted && p.PaidAt >= FromDate.Value.Date) ||
                (p.IsReverted && p.RevertedAt.HasValue && p.RevertedAt.Value.Date >= FromDate.Value.Date));

        if (ToDate.HasValue)
            query = query.Where(p =>
                (!p.IsReverted && p.PaidAt < ToDate.Value.Date.AddDays(1)) ||
                (p.IsReverted && p.RevertedAt.HasValue && p.RevertedAt.Value.Date < ToDate.Value.Date.AddDays(1)));

        if (CollectorId.HasValue)
            query = query.Where(p => p.CollectorId == CollectorId.Value);

        if (CheckpointId.HasValue)
            query = query.Where(p => p.CheckpointId == CheckpointId.Value);

        if (MovementId.HasValue)
            query = query.Where(p => p.MovementId == MovementId.Value);

        return query;
    }

    private void ComputeSummaries()
    {
        var data = GetFilteredQuery().ToList();

        TotalTransactions = data.Count;
        CompletedCount = data.Count(p => !p.IsReverted);
        CompletedAmount = data.Where(p => !p.IsReverted).Sum(p => p.Amount);
        CancelledCount = data.Count(p => p.IsReverted);
        CancelledAmount = data.Where(p => p.IsReverted).Sum(p => p.Amount);

        StatusSummaries = new List<StatusSummary>
        {
            new StatusSummary { Status = "Completed", Count = CompletedCount, Amount = CompletedAmount },
            new StatusSummary { Status = "Cancelled", Count = CancelledCount, Amount = CancelledAmount }
        };

        CollectorSummaries = data
            .GroupBy(p => p.Collector?.Username ?? "Unassigned")
            .Select(g => new CollectorSummary
            {
                CollectorName = g.Key,
                Completed = g.Count(p => !p.IsReverted),
                CompletedAmount = g.Where(p => !p.IsReverted).Sum(p => p.Amount),
                Cancelled = g.Count(p => p.IsReverted),
                CancelledAmount = g.Where(p => p.IsReverted).Sum(p => p.Amount)
            })
            .OrderByDescending(x => x.CompletedAmount)
            .ToList();

        MovementSummaries = data
            .GroupBy(p => p.Movement?.Name ?? p.MovementType)
            .Select(g => new MovementSummary
            {
                MovementName = g.Key,
                Completed = g.Count(p => !p.IsReverted),
                CompletedAmount = g.Where(p => !p.IsReverted).Sum(p => p.Amount),
                Cancelled = g.Count(p => p.IsReverted),
                CancelledAmount = g.Where(p => p.IsReverted).Sum(p => p.Amount)
            })
            .OrderByDescending(x => x.CompletedAmount)
            .ToList();

        CheckpointSummaries = data
            .GroupBy(p => p.Checkpoint?.Name ?? "Unassigned")
            .Select(g => new CheckpointSummary
            {
                CheckpointName = g.Key,
                Completed = g.Count(p => !p.IsReverted),
                CompletedAmount = g.Where(p => !p.IsReverted).Sum(p => p.Amount),
                Cancelled = g.Count(p => p.IsReverted),
                CancelledAmount = g.Where(p => p.IsReverted).Sum(p => p.Amount)
            })
            .OrderByDescending(x => x.CompletedAmount)
            .ToList();
// Per-payment detail records — include the audit Reference No. (or NULL for unrecorded)
        PaymentRecords = data
            .Select(p => new PaymentRecord
            {
                ReferenceNo = p.ReferenceNo,
                Id = p.Id,
                InvoiceId = p.InvoiceNumber,
                PlateNumber = p.Vehicle != null ? p.Vehicle.PlateNumber : "N/A",
                MovementType = p.MovementType,
                CollectorName = p.Collector?.Username ?? "Unassigned",
                Amount = p.Amount,
                PaidAt = p.IsReverted ? (p.RevertedAt ?? p.PaidAt) : p.PaidAt,
                IsReverted = p.IsReverted
            })
            .OrderByDescending(p => p.ReferenceNo.HasValue)
            .ThenByDescending(p => p.Id)
            .ToList();
    }

    public IActionResult OnGetExportCsv()
    {
        LoadDropdowns();
        ComputeSummaries();

        var sb = new StringBuilder();
        sb.AppendLine("Summary Report");
        sb.AppendLine($"From,{FromDate?.ToString("yyyy-MM-dd") ?? "All"}");
        sb.AppendLine($"To,{ToDate?.ToString("yyyy-MM-dd") ?? "All"}");
        sb.AppendLine($"Total Transactions,{TotalTransactions}");
        sb.AppendLine($"Completed Count,{CompletedCount}");
        sb.AppendLine($"Completed Amount,{CompletedAmount:N0}");
        sb.AppendLine($"Cancelled Count,{CancelledCount}");
        sb.AppendLine($"Cancelled Amount,{CancelledAmount:N0}");
        sb.AppendLine($"Net Amount,{NetAmount:N0}");
        sb.AppendLine();
        sb.AppendLine("Collector,Completed,Completed Amount,Cancelled,Cancelled Amount");

        foreach (var c in CollectorSummaries)
        {
            sb.AppendLine($"{c.CollectorName},{c.Completed},{c.CompletedAmount:N0},{c.Cancelled},{c.CancelledAmount:N0}");
        }

        // Per-payment detail — include the audit Reference No. (or blank/for unrecorded)
        sb.AppendLine();
        sb.AppendLine("Ref No,Payment ID,Invoice ID,Plate,Movement,Collector,Amount,Date,Status");

        foreach (var p in PaymentRecords)
        {
            var refNo = p.ReferenceNo?.ToString() ?? "";
            var status = p.IsReverted ? "Cancelled" : "Completed";
            var date = p.PaidAt.ToString("yyyy-MM-dd HH:mm");
            sb.AppendLine($"{refNo},{p.Id},{p.InvoiceId},{p.PlateNumber},{p.MovementType},{p.CollectorName},{p.Amount:N0},{date},{status}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"SummaryReport_{AppTime.Now:yyyyMMddHHmmss}.csv");
    }
}
