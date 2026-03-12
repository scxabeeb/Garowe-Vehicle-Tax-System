using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports
{
    public class TaxAmountReportModel : PageModel
    {
        private readonly AppDbContext _context;

        public TaxAmountReportModel(AppDbContext context)
        {
            _context = context;
        }

        public List<TaxAmount> TaxAmounts { get; set; } = new();

        // Filters
        [BindProperty(SupportsGet = true)]
        public int? CarTypeId { get; set; }

        // Change from MovementId to MovementName
        [BindProperty(SupportsGet = true)]
        public string? MovementName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        // Pagination
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }

        // Dropdown data
        public List<CarType> CarTypes { get; set; } = new();
        public List<Movement> Movements { get; set; } = new();

        public void OnGet()
        {
            CarTypes = _context.CarTypes
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToList();

            // Load all movements and take only one per NAME
            var allMovements = _context.Movements
                .AsNoTracking()
                .OrderBy(m => m.Name)
                .ToList();

            Movements = allMovements
                .GroupBy(m => m.Name)
                .Select(g => g.First())
                .ToList();

            var query = _context.TaxAmounts
                .Include(t => t.CarType)
                .Include(t => t.Movement)
                .AsQueryable();

            // Search
            if (!string.IsNullOrWhiteSpace(Search))
            {
                query = query.Where(t =>
                    t.CarType!.Name.Contains(Search) ||
                    t.Movement!.Name.Contains(Search));
            }

            // Filters
            if (CarTypeId.HasValue)
                query = query.Where(t => t.CarTypeId == CarTypeId.Value);

            // Filter by Movement NAME (this fixes your problem)
            if (!string.IsNullOrWhiteSpace(MovementName))
                query = query.Where(t => t.Movement!.Name == MovementName);

            int totalCount = query.Count();
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            TaxAmounts = query
                .OrderBy(t => t.CarType!.Name)
                .ThenBy(t => t.Movement!.Name)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }

        public IActionResult OnGetExportCsv(int? carTypeId, string? movementName, string? search)
        {
            var query = _context.TaxAmounts
                .Include(t => t.CarType)
                .Include(t => t.Movement)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(t =>
                    t.CarType!.Name.Contains(search) ||
                    t.Movement!.Name.Contains(search));

            if (carTypeId.HasValue)
                query = query.Where(t => t.CarTypeId == carTypeId.Value);

            if (!string.IsNullOrWhiteSpace(movementName))
                query = query.Where(t => t.Movement!.Name == movementName);

            var list = query.ToList();

            var csv = "Id,CarType,Movement,Amount\n";
            foreach (var t in list)
            {
                csv += $"{t.Id},{t.CarType?.Name},{t.Movement?.Name},{t.Amount}\n";
            }

            return File(
                System.Text.Encoding.UTF8.GetBytes(csv),
                "text/csv",
                "TaxAmountReport.csv"
            );
        }
    }
}
