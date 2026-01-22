using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

public class ReceiptReferencesModel : PageModel
{
    private readonly AppDbContext _context;

    public ReceiptReferencesModel(AppDbContext context)
    {
        _context = context;
    }

    public List<ReceiptReference> ReceiptReferences { get; set; } = new();

    // 🔍 Global Search
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    // Filters
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? VehicleId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Collector { get; set; }

    // Date Filters
    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    // Dropdown Data
    public List<Vehicle> Vehicles { get; set; } = new();
    public List<string> Collectors { get; set; } = new();

    // Pagination
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    public int TotalRecords { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalRecords / (double)PageSize);

    public async Task OnGetAsync()
    {
        Vehicles = await _context.Vehicles
            .OrderBy(v => v.PlateNumber)
            .ToListAsync();

        // Only collectors who actually collected
        Collectors = await _context.ReceiptReferences
            .Where(r => !string.IsNullOrEmpty(r.UsedBy))
            .Select(r => r.UsedBy!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        var query = _context.ReceiptReferences
            .Include(r => r.Vehicle)
            .AsQueryable();

        // 🔍 Global Search
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(r =>
                r.ReferenceNumber.Contains(Search) ||
                (r.Vehicle != null && r.Vehicle.PlateNumber.Contains(Search)) ||
                (r.UsedBy != null && r.UsedBy.Contains(Search)) ||
                (r.CancelledBy != null && r.CancelledBy.Contains(Search)) ||
                (r.CancelledReason != null && r.CancelledReason.Contains(Search))
            );
        }

        // Filters
        if (VehicleId.HasValue)
            query = query.Where(r => r.VehicleId == VehicleId.Value);

        if (!string.IsNullOrWhiteSpace(Collector))
            query = query.Where(r => r.UsedBy == Collector);

        if (!string.IsNullOrWhiteSpace(Status))
        {
            query = Status switch
            {
                "Available" => query.Where(r => !r.IsUsed && !r.IsCancelled),
                "Used" => query.Where(r => r.IsUsed),
                "Cancelled" => query.Where(r => r.IsCancelled),
                _ => query
            };
        }

        // 📅 Date Filters (by UsedAt)
        if (FromDate.HasValue)
            query = query.Where(r => r.UsedAt >= FromDate.Value);

        if (ToDate.HasValue)
        {
            var endDate = ToDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(r => r.UsedAt <= endDate);
        }

        TotalRecords = await query.CountAsync();

        ReceiptReferences = await query
            .OrderByDescending(r => r.Id)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    // 📤 CSV Export
    public async Task<IActionResult> OnGetExportCsvAsync(
        string? search,
        string? status,
        int? vehicleId,
        string? collector,
        DateTime? fromDate,
        DateTime? toDate)
    {
        var query = _context.ReceiptReferences
            .Include(r => r.Vehicle)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r =>
                r.ReferenceNumber.Contains(search) ||
                (r.Vehicle != null && r.Vehicle.PlateNumber.Contains(search)) ||
                (r.UsedBy != null && r.UsedBy.Contains(search)) ||
                (r.CancelledBy != null && r.CancelledBy.Contains(search)) ||
                (r.CancelledReason != null && r.CancelledReason.Contains(search))
            );
        }

        if (vehicleId.HasValue)
            query = query.Where(r => r.VehicleId == vehicleId.Value);

        if (!string.IsNullOrWhiteSpace(collector))
            query = query.Where(r => r.UsedBy == collector);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = status switch
            {
                "Available" => query.Where(r => !r.IsUsed && !r.IsCancelled),
                "Used" => query.Where(r => r.IsUsed),
                "Cancelled" => query.Where(r => r.IsCancelled),
                _ => query
            };
        }

        if (fromDate.HasValue)
            query = query.Where(r => r.UsedAt >= fromDate.Value);

        if (toDate.HasValue)
        {
            var endDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(r => r.UsedAt <= endDate);
        }

        var data = await query.ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Id,Reference,Status,VehiclePlate,Collector,UsedAt,CancelledBy,CancelledReason");

        foreach (var r in data)
        {
            var statusText = r.IsCancelled ? "Cancelled" : r.IsUsed ? "Used" : "Available";
            sb.AppendLine(
                $"{r.Id}," +
                $"{r.ReferenceNumber}," +
                $"{statusText}," +
                $"{r.Vehicle?.PlateNumber}," +
                $"{r.UsedBy}," +
                $"{r.UsedAt}," +
                $"{r.CancelledBy}," +
                $"{r.CancelledReason}"
            );
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "ReceiptReferenceReport.csv");
    }
}
