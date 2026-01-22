using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

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
    public int Page { get; set; } = 1;

    public List<User> Users { get; set; } = new();
    public List<Payment> Transactions { get; set; } = new();

    public void OnGet()
    {
        Users = _context.Users.OrderBy(u => u.Username).ToList();

        var query = _context.Payments
            .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
            .Include(p => p.ReceiptReference)
            .Include(p => p.RevertedByUser)
            .Include(p => p.Collector)
            .AsQueryable();

        // Filters
        if (FromDate.HasValue)
            query = query.Where(p => p.PaidAt.Date >= FromDate.Value.Date);

        if (ToDate.HasValue)
            query = query.Where(p => p.PaidAt.Date <= ToDate.Value.Date);

        if (UserId.HasValue)
            query = query.Where(p =>
                p.CollectorId == UserId.Value ||
                p.RevertedByUserId == UserId.Value);

        if (!string.IsNullOrEmpty(Action) && Action != "All")
        {
            if (Action == "Completed")
                query = query.Where(p => !p.IsReverted);

            if (Action == "Reverted")
                query = query.Where(p => p.IsReverted);
        }

        query = query.OrderByDescending(p => p.PaidAt);

        // PageSize = 0 means show all
        if (PageSize > 0)
        {
            query = query
                .Skip((Page - 1) * PageSize)
                .Take(PageSize);
        }

        Transactions = query.ToList();
    }

    // EXPORT ONLY FILTERED DATA (ignores paging)
    public IActionResult OnGetExportFiltered()
    {
        var query = _context.Payments
            .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
            .Include(p => p.ReceiptReference)
            .Include(p => p.RevertedByUser)
            .Include(p => p.Collector)
            .AsQueryable();

        // Same filters as OnGet
        if (FromDate.HasValue)
            query = query.Where(p => p.PaidAt.Date >= FromDate.Value.Date);

        if (ToDate.HasValue)
            query = query.Where(p => p.PaidAt.Date <= ToDate.Value.Date);

        if (UserId.HasValue)
            query = query.Where(p =>
                p.CollectorId == UserId.Value ||
                p.RevertedByUserId == UserId.Value);

        if (!string.IsNullOrEmpty(Action) && Action != "All")
        {
            if (Action == "Completed")
                query = query.Where(p => !p.IsReverted);

            if (Action == "Reverted")
                query = query.Where(p => p.IsReverted);
        }

        var data = query
            .OrderByDescending(p => p.PaidAt)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Date,Plate,Car Type,Movement,Collector,Amount,Receipt,Status");

        foreach (var t in data)
        {
            sb.AppendLine(string.Join(",",
                Escape(t.PaidAt.ToLocalTime().ToString("dd MMM yyyy HH:mm")),
                Escape(t.Vehicle?.PlateNumber),
                Escape(t.Vehicle?.CarType?.Name),
                Escape(t.MovementType),
                Escape(t.Collector?.Username ?? "System"),
                Escape(t.Amount.ToString()),
                Escape(t.ReceiptReference?.ReferenceNumber),
                Escape(t.IsReverted ? "Reverted" : "Completed")
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"Transactions_{DateTime.Now:yyyyMMdd_HHmm}.csv");
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
