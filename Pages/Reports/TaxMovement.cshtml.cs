using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Pages.Reports
{
    public class TaxMovementModel : PageModel
    {
        private readonly AppDbContext _context;
        public TaxMovementModel(AppDbContext context)
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

        // Pagination
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 20;

        public int TotalPages { get; set; }

        // Data
        public decimal TotalTax { get; set; }
        public List<TaxByMovement> MovementTotals { get; set; } = new();

        public async Task OnGetAsync()
        {
            var query = _context.Payments
                .Include(p => p.Movement)
                .Where(p => !p.IsReverted);

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt <= ToDate.Value);

            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

            // Total Tax
            TotalTax = await query.SumAsync(p => p.Amount);

            // Grouping
            var groupedQuery = query
                .GroupBy(p => new { p.MovementId, p.Movement!.Name })
                .Select(g => new TaxByMovement
                {
                    MovementId = g.Key.MovementId,
                    MovementName = g.Key.Name,
                    TotalAmount = g.Sum(x => x.Amount),
                    TotalPayments = g.Count()
                });

            var totalCount = await groupedQuery.CountAsync();
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            MovementTotals = await groupedQuery
                .OrderByDescending(x => x.TotalAmount)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }

        // Export to CSV
        public async Task<IActionResult> OnGetExportAsync()
        {
            var query = _context.Payments
                .Include(p => p.Movement)
                .Where(p => !p.IsReverted);

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt <= ToDate.Value);

            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

            var data = await query
                .GroupBy(p => new { p.MovementId, p.Movement!.Name })
                .Select(g => new TaxByMovement
                {
                    MovementId = g.Key.MovementId,
                    MovementName = g.Key.Name,
                    TotalAmount = g.Sum(x => x.Amount),
                    TotalPayments = g.Count()
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Movement,Total Payments,Total Amount");

            foreach (var item in data)
            {
                sb.AppendLine($"{item.MovementName},{item.TotalPayments},{item.TotalAmount}");
            }

            return File(
                Encoding.UTF8.GetBytes(sb.ToString()),
                "text/csv",
                $"TaxByMovement_{DateTime.Now:yyyyMMddHHmmss}.csv"
            );
        }
    }

    public class TaxByMovement
    {
        public int MovementId { get; set; }
        public string MovementName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public int TotalPayments { get; set; }
    }
}
