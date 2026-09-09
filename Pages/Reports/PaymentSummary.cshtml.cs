using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

/// <summary>
/// Payment Summary Report — payments grouped by System ID and Status.
/// Provides a clear view of payment distribution across different system identifiers
/// and their current status (Paid, Pending, Reverted).
/// </summary>
public class PaymentSummaryModel : PageModel
{
    private readonly AppDbContext _context;

    public PaymentSummaryModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? SystemId { get; set; }

    public List<SummaryRow> Rows { get; set; } = new();
    public int TotalTransactions { get; set; }
    public decimal TotalAmount { get; set; }
    public int UniqueSystemIds { get; set; }
    public int UniqueStatuses { get; set; }
    public string? Message { get; private set; }
    public string? ErrorMessage { get; private set; }

    public class SummaryRow
    {
        public string? SystemId { get; set; }
        public string Status { get; set; } = "";
        public int Transactions { get; set; }
        public decimal TotalAmount { get; set; }
        public double Percentage { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Only authorized users (Admin, Manager, Accountant, Finance)
        var isAuthorized = User.IsInRole("Admin") || User.IsInRole("Manager")
            || User.IsInRole("Accountant") || User.HasClaim("permission", "reports.view");
        if (!isAuthorized) return RedirectToPage("/Index");

        FromDate ??= AppTime.Today.AddDays(-30);
        ToDate ??= AppTime.Today;

        var from = FromDate.Value.Date;
        var to = ToDate.Value.Date.AddDays(1);

        var query = _context.Payments
            .Where(p => p.PaidAt >= from && p.PaidAt < to)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Status))
        {
            if (Status == "Paid") query = query.Where(p => p.IsPaid && !p.IsReverted);
            else if (Status == "Reverted") query = query.Where(p => p.IsReverted);
            else if (Status == "Pending") query = query.Where(p => !p.IsPaid && !p.IsReverted);
        }

        if (!string.IsNullOrWhiteSpace(SystemId))
        {
            var sysId = SystemId.Trim();
            query = query.Where(p => p.TransactionId.Contains(sysId) || p.InvoiceNumber.Contains(sysId));
        }

        var payments = await query
            .OrderBy(p => p.TransactionId)
            .ThenBy(p => p.IsPaid)
            .ThenBy(p => p.IsReverted)
            .ToListAsync();

        // Group by System ID (TransactionId) and Status
        var grouped = payments
            .GroupBy(p => new { SystemId = p.TransactionId ?? "N/A", Status = GetStatus(p) })
            .Select(g => new SummaryRow
            {
                SystemId = g.Key.SystemId,
                Status = g.Key.Status,
                Transactions = g.Count(),
                TotalAmount = g.Sum(p => p.Amount),
            })
            .OrderBy(r => r.SystemId)
            .ThenBy(r => r.Status)
            .ToList();

        TotalTransactions = grouped.Sum(r => r.Transactions);
        TotalAmount = grouped.Sum(r => r.TotalAmount);
        UniqueSystemIds = grouped.Select(r => r.SystemId).Distinct().Count();
        UniqueStatuses = grouped.Select(r => r.Status).Distinct().Count();

        // Calculate percentages
        foreach (var row in grouped)
        {
            row.Percentage = TotalAmount > 0 ? (double)(row.TotalAmount / TotalAmount) * 100 : 0;
        }

        Rows = grouped;
        return Page();
    }

    private static string GetStatus(Payment p)
    {
        if (p.IsReverted) return "Reverted";
        if (p.IsPaid) return "Paid";
        return "Pending";
    }
}
