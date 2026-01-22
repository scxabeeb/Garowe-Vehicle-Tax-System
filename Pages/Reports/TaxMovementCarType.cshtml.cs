using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Pages.Reports
{
    public class TaxMovementCarTypeModel : PageModel
    {
        private readonly AppDbContext _context;
        public TaxMovementCarTypeModel(AppDbContext context)
        {
            _context = context;
        }

        // Filters
        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? MovementId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CarTypeId { get; set; }

        // Pagination
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 20;

        public int TotalPages { get; set; }

        // Data
        public decimal TotalTax { get; set; }
        public List<TaxByMovementCarType> ReportData { get; set; } = new();

        public async Task OnGetAsync()
        {
            var query = _context.Payments
                .Include(p => p.Movement)
                .Include(p => p.Vehicle)
                    .ThenInclude(v => v.CarType)
                .Where(p => !p.IsReverted);

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt <= ToDate.Value);

            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle!.CarTypeId == CarTypeId.Value);

            // Total Amount
            TotalTax = await query.SumAsync(p => p.Amount);

            // Group by Movement + CarType
            var groupedQuery = query
                .GroupBy(p => new
                {
                    p.MovementId,
                    MovementName = p.Movement!.Name,
                    p.Vehicle!.CarTypeId,
                    CarTypeName = p.Vehicle.CarType!.Name
                })
                .Select(g => new TaxByMovementCarType
                {
                    MovementId = g.Key.MovementId,
                    MovementName = g.Key.MovementName,
                    CarTypeId = g.Key.CarTypeId,
                    CarTypeName = g.Key.CarTypeName,
                    TotalAmount = g.Sum(x => x.Amount),
                    TotalPayments = g.Count()
                });

            var count = await groupedQuery.CountAsync();
            TotalPages = (int)Math.Ceiling(count / (double)PageSize);

            ReportData = await groupedQuery
                .OrderByDescending(x => x.TotalAmount)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        // Export CSV
        public async Task<IActionResult> OnGetExportAsync()
        {
            var query = _context.Payments
                .Include(p => p.Movement)
                .Include(p => p.Vehicle)
                    .ThenInclude(v => v.CarType)
                .Where(p => !p.IsReverted);

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt <= ToDate.Value);

            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle!.CarTypeId == CarTypeId.Value);

            var data = await query
                .GroupBy(p => new
                {
                    p.MovementId,
                    MovementName = p.Movement!.Name,
                    p.Vehicle!.CarTypeId,
                    CarTypeName = p.Vehicle.CarType!.Name
                })
                .Select(g => new TaxByMovementCarType
                {
                    MovementName = g.Key.MovementName,
                    CarTypeName = g.Key.CarTypeName,
                    TotalAmount = g.Sum(x => x.Amount),
                    TotalPayments = g.Count()
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Movement,Car Type,Total Payments,Total Amount");

            foreach (var item in data)
            {
                sb.AppendLine($"{item.MovementName},{item.CarTypeName},{item.TotalPayments},{item.TotalAmount}");
            }

            return File(
                Encoding.UTF8.GetBytes(sb.ToString()),
                "text/csv",
                $"Tax_Movement_CarType_{DateTime.Now:yyyyMMddHHmmss}.csv"
            );
        }
    }

    public class TaxByMovementCarType
    {
        public int MovementId { get; set; }
        public string MovementName { get; set; } = "";
        public int CarTypeId { get; set; }
        public string CarTypeName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public int TotalPayments { get; set; }
    }
}
