using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Pages.Reports
{
    public class TaxModel : PageModel
    {
        private readonly AppDbContext _context;

        public TaxModel(AppDbContext context)
        {
            _context = context;
        }

        // Filters
        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? PlateNumber { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CarTypeId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CollectorId { get; set; }

        // Dropdowns
        public List<SelectListItem> CarTypes { get; set; } = new();
        public List<SelectListItem> Collectors { get; set; } = new();

        // Results
        public List<TaxReportDto> TaxReports { get; set; } = new();

        // Summary
        public decimal TotalAmount { get; set; }
        public int TotalPayments { get; set; }
        public int TotalVehicles { get; set; }

        public async Task OnGetAsync()
        {
            CarTypes = await _context.CarTypes
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();

            Collectors = await _context.Users
                .Where(u => u.Role == "Collector")
                .Select(u => new SelectListItem
                {
                    Value = u.Id.ToString(),
                    Text = u.Username
                })
                .ToListAsync();

            var query = _context.Payments
                .Include(p => p.Vehicle)
                    .ThenInclude(v => v.CarType)
                .Include(p => p.Collector)
                .Where(p => !p.IsReverted)
                .AsQueryable();

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt <= ToDate.Value);

            if (!string.IsNullOrWhiteSpace(PlateNumber))
                query = query.Where(p => p.Vehicle!.PlateNumber.Contains(PlateNumber));

            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle!.CarTypeId == CarTypeId.Value);

            if (CollectorId.HasValue)
                query = query.Where(p => p.CollectorId == CollectorId.Value);

            TaxReports = await query
                .OrderByDescending(p => p.PaidAt)
                .Select(p => new TaxReportDto
                {
                    Date = p.PaidAt,
                    PlateNumber = p.Vehicle!.PlateNumber,
                    CarType = p.Vehicle!.CarType!.Name,
                    Amount = p.Amount,
                    Collector = p.Collector != null ? p.Collector.Username : "System",
                    Reference = p.ReceiptReference != null
                        ? p.ReceiptReference.ReferenceNumber
                        : "-"
                })
                .ToListAsync();

            TotalAmount = TaxReports.Sum(x => x.Amount);
            TotalPayments = TaxReports.Count;
            TotalVehicles = TaxReports.Select(x => x.PlateNumber).Distinct().Count();
        }
    }

    public class TaxReportDto
    {
        public DateTime Date { get; set; }
        public string PlateNumber { get; set; } = "";
        public string CarType { get; set; } = "";
        public decimal Amount { get; set; }
        public string Collector { get; set; } = "";
        public string Reference { get; set; } = "";
    }
}
