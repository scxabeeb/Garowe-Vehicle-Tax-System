using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    // ===== FILTERS =====
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public string? PlateNumber { get; set; }
    [BindProperty(SupportsGet = true)] public int? CarTypeId { get; set; }
    [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }

    // ===== PAGINATION =====
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }

    // ===== DATA =====
    public List<Payment> Payments { get; set; } = new();
    public SelectList CarTypes { get; set; } = null!;
    public SelectList Movements { get; set; } = null!;

    // ===== TOTALS =====
    public int TotalVehicles { get; set; }
    public int TotalPayments { get; set; }
    public decimal TotalAmount { get; set; }

    public async Task OnGetAsync()
    {
        CarTypes = new SelectList(_context.CarTypes, "Id", "Name");
        Movements = new SelectList(_context.Movements, "Id", "Name");

        // Base query: ONLY non-reverted payments
        var query = _context.Payments
            .Where(p => !p.IsReverted)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.CarType)
            .Include(p => p.Movement)
            .Include(p => p.ReceiptReference)
            .AsQueryable();

        // 🔴 DATE FILTER (NO .Date in LINQ)
        if (FromDate.HasValue)
        {
            var from = FromDate.Value.Date;
            query = query.Where(p => p.PaidAt >= from);
        }

        if (ToDate.HasValue)
        {
            var to = ToDate.Value.Date.AddDays(1);
            query = query.Where(p => p.PaidAt < to);
        }

        // Plate filter
        if (!string.IsNullOrWhiteSpace(PlateNumber))
            query = query.Where(p =>
                p.Vehicle != null &&
                p.Vehicle.PlateNumber.Contains(PlateNumber));

        // Car type filter
        if (CarTypeId.HasValue && CarTypeId > 0)
            query = query.Where(p =>
                p.Vehicle != null &&
                p.Vehicle.CarTypeId == CarTypeId);

        // Movement filter
        if (MovementId.HasValue && MovementId > 0)
            query = query.Where(p => p.MovementId == MovementId);

        // ===== TOTALS =====
        TotalPayments = await query.CountAsync();
        TotalAmount = await query.SumAsync(p => (decimal?)p.Amount) ?? 0;
        TotalVehicles = await query.Select(p => p.VehicleId).Distinct().CountAsync();

        TotalPages = (int)Math.Ceiling(TotalPayments / (double)PageSize);

        // ===== PAGED DATA =====
        Payments = await query
            .OrderByDescending(p => p.PaidAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    // ===== EXCEL EXPORT =====
    public async Task<IActionResult> OnPostExportExcelAsync()
    {
        ExcelPackage.License.SetNonCommercialOrganization("Garowe Municipality");

        var query = _context.Payments
            .Where(p => !p.IsReverted)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.CarType)
            .Include(p => p.Movement)
            .Include(p => p.ReceiptReference)
            .AsQueryable();

        // Same filters as OnGet

        if (FromDate.HasValue)
        {
            var from = FromDate.Value.Date;
            query = query.Where(p => p.PaidAt >= from);
        }

        if (ToDate.HasValue)
        {
            var to = ToDate.Value.Date.AddDays(1);
            query = query.Where(p => p.PaidAt < to);
        }

        if (!string.IsNullOrWhiteSpace(PlateNumber))
            query = query.Where(p =>
                p.Vehicle != null &&
                p.Vehicle.PlateNumber.Contains(PlateNumber));

        if (CarTypeId.HasValue && CarTypeId > 0)
            query = query.Where(p =>
                p.Vehicle != null &&
                p.Vehicle.CarTypeId == CarTypeId);

        if (MovementId.HasValue && MovementId > 0)
            query = query.Where(p => p.MovementId == MovementId);

        var data = await query
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync();

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Vehicle Tax Report");

        string[] headers =
        {
            "Date", "Plate", "Owner", "Mobile",
            "Car Type", "Movement", "Receipt Ref", "Amount"
        };

        for (int i = 0; i < headers.Length; i++)
            sheet.Cells[1, i + 1].Value = headers[i];

        sheet.Cells[1, 1, 1, headers.Length].Style.Font.Bold = true;

        int row = 2;
        foreach (var p in data)
        {
            sheet.Cells[row, 1].Value = p.PaidAt.ToString("yyyy-MM-dd");
            sheet.Cells[row, 2].Value = p.Vehicle?.PlateNumber;
            sheet.Cells[row, 3].Value = p.Vehicle?.OwnerName;
            sheet.Cells[row, 4].Value = p.Vehicle?.Mobile;
            sheet.Cells[row, 5].Value = p.Vehicle?.CarType?.Name;
            sheet.Cells[row, 6].Value = p.Movement?.Name;
            sheet.Cells[row, 7].Value = p.ReceiptReference?.ReferenceNumber;
            sheet.Cells[row, 8].Value = p.Amount;
            row++;
        }

        sheet.Cells.AutoFitColumns();

        return File(
            package.GetAsByteArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"VehicleTaxReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
        );
    }
}
