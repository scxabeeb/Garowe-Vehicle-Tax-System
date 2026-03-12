using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
<<<<<<< HEAD
using Microsoft.AspNetCore.Mvc.Rendering;
=======
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web.Data;

namespace VehicleTax.Web.Pages.Reports
{
    public class TaxMovementCarTypeModel : PageModel
    {
        private readonly AppDbContext _context;
<<<<<<< HEAD

=======
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
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
<<<<<<< HEAD
        public int? CarTypeId { get; set; }

        // Paging
=======
        public int? MovementId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? CarTypeId { get; set; }

        // Pagination
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 20;

        public int TotalPages { get; set; }

        // Data
        public decimal TotalTax { get; set; }
        public List<TaxByMovementCarType> ReportData { get; set; } = new();

<<<<<<< HEAD
        public SelectList CarTypes { get; set; } = null!;

        public async Task OnGetAsync()
        {
            CarTypes = new SelectList(
                await _context.CarTypes
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                "Id",
                "Name"
            );

=======
        public async Task OnGetAsync()
        {
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
            var query = _context.Payments
                .Include(p => p.Movement)
                .Include(p => p.Vehicle)
                    .ThenInclude(v => v.CarType)
                .Where(p => !p.IsReverted);

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt <= ToDate.Value);

<<<<<<< HEAD
            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle!.CarTypeId == CarTypeId.Value);

            // Total tax
            TotalTax = await query.SumAsync(p => p.Amount);

            // Correct grouping (NO duplicate names)
            var groupedQuery = query
                .GroupBy(p => new
                {
                    MovementName = p.Movement!.Name,
                    CarTypeName = p.Vehicle!.CarType!.Name
                })
                .Select(g => new TaxByMovementCarType
                {
                    MovementName = g.Key.MovementName,
=======
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
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
                    CarTypeName = g.Key.CarTypeName,
                    TotalAmount = g.Sum(x => x.Amount),
                    TotalPayments = g.Count()
                });

            var count = await groupedQuery.CountAsync();
            TotalPages = (int)Math.Ceiling(count / (double)PageSize);

            ReportData = await groupedQuery
                .OrderByDescending(x => x.TotalAmount)
                .Skip((PageNumber - 1) * PageSize)
<<<<<<< HEAD
                .Take(PageSize == 999999 ? count : PageSize)
                .ToListAsync();
        }

        // CSV Export
=======
                .Take(PageSize)
                .ToListAsync();
        }

        // Export CSV
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
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

<<<<<<< HEAD
=======
            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle!.CarTypeId == CarTypeId.Value);

            var data = await query
                .GroupBy(p => new
                {
<<<<<<< HEAD
                    MovementName = p.Movement!.Name,
                    CarTypeName = p.Vehicle!.CarType!.Name
=======
                    p.MovementId,
                    MovementName = p.Movement!.Name,
                    p.Vehicle!.CarTypeId,
                    CarTypeName = p.Vehicle.CarType!.Name
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
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
<<<<<<< HEAD
        public string MovementName { get; set; } = "";
=======
        public int MovementId { get; set; }
        public string MovementName { get; set; } = "";
        public int CarTypeId { get; set; }
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
        public string CarTypeName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public int TotalPayments { get; set; }
    }
}
