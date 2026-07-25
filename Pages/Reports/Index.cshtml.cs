using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports
{
    public class CollectorSummary
    {
        public string CollectorName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public int TotalPayments { get; set; }
    }

    public class MovementSummary
    {
        public string MovementName { get; set; } = "";
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class CarTypeSummary
    {
        public string CarTypeName { get; set; } = "";
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class DailyCollectionSummary
    {
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        // Filters
        [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? PlateNumber { get; set; }
        [BindProperty(SupportsGet = true)] public int? CarTypeId { get; set; }
        [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }
        [BindProperty(SupportsGet = true)] public int? CollectorId { get; set; }

        // Pagination
        [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 10;
        [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }

        // Data
        public List<Payment> Payments { get; set; } = new();
        public SelectList CarTypes { get; set; } = null!;
        public SelectList Movements { get; set; } = null!;
        public SelectList Collectors { get; set; } = null!;
        public List<CollectorSummary> CollectorSummaries { get; set; } = new();
        public List<MovementSummary> MovementSummaries { get; set; } = new();
        public List<CarTypeSummary> CarTypeSummaries { get; set; } = new();
        public List<DailyCollectionSummary> DailySummaries { get; set; } = new();

        // Totals
        public int TotalVehicles { get; set; }
        public int TotalPayments { get; set; }
        public decimal TotalAmount { get; set; }

        public async Task OnGetAsync()
        {
            // Car Types
            CarTypes = new SelectList(
                await _context.CarTypes
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                "Id",
                "Name"
            );

            // Movements (UNIQUE BY NAME, even if many vehicles/payments use it)
            Movements = new SelectList(
                await _context.Movements
                    .AsNoTracking()
                    .GroupBy(m => m.Name)
                    .Select(g => new
                    {
                        Id = g.Min(x => x.Id),   // take one Id per movement name
                        Name = g.Key
                    })
                    .OrderBy(x => x.Name)
                    .ToListAsync(),
                "Id",
                "Name"
            );

            // Collectors
            Collectors = new SelectList(
                await _context.Users
                    .AsNoTracking()
                    .OrderBy(u => u.Username)
                    .ToListAsync(),
                "Id",
                "Username"
            );

            var query = _context.Payments
                .Where(p => p.IsPaid && !p.IsReverted)
                .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
                .Include(p => p.Movement)
                .Include(p => p.ReceiptReference)
                .Include(p => p.Collector)
                .AsQueryable();

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value.Date);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(PlateNumber))
                query = query.Where(p => p.Vehicle != null && p.Vehicle.PlateNumber.Contains(PlateNumber));

            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle != null && p.Vehicle.CarTypeId == CarTypeId.Value);

            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

            if (CollectorId.HasValue)
                query = query.Where(p => p.CollectorId == CollectorId.Value);

            // Totals
            TotalPayments = await query.CountAsync();
            TotalAmount = await query.SumAsync(p => (decimal?)p.Amount) ?? 0;
            TotalVehicles = await query.Select(p => p.VehicleId).Distinct().CountAsync();

            // Collector summary
            CollectorSummaries = await query
                .Where(p => p.Collector != null)
                .GroupBy(p => p.Collector!.Username)
                .Select(g => new CollectorSummary
                {
                    CollectorName = g.Key,
                    TotalPayments = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .ToListAsync();

            // Movement infographic summary
            MovementSummaries = await query
                .GroupBy(p => p.Movement != null ? p.Movement.Name : p.MovementType)
                .Select(g => new MovementSummary
                {
                    MovementName = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .Take(5)
                .ToListAsync();

            // Car type infographic summary
            CarTypeSummaries = await query
                .Where(p => p.Vehicle != null && p.Vehicle.CarType != null)
                .GroupBy(p => p.Vehicle!.CarType!.Name)
                .Select(g => new CarTypeSummary
                {
                    CarTypeName = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.TotalAmount)
                .Take(5)
                .ToListAsync();

            // 7-day daily collection trend
            var startDate = AppTime.Today.AddDays(-6);
            var startDateUtc = AppTime.GetUtcDayRange(startDate).StartUtc;
            var dailyRaw = await query
                .Where(p => p.PaidAt >= startDateUtc)
                .Select(p => new
                {
                    p.PaidAt,
                    p.Amount
                })
                .ToListAsync();

            var dailySummaries = dailyRaw
                .GroupBy(p => AppTime.ToLocal(p.PaidAt).Date)
                .Select(g => new DailyCollectionSummary
                {
                    Date = g.Key,
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .ToList();

            DailySummaries = Enumerable.Range(0, 7)
                .Select(i => startDate.AddDays(i))
                .Select(d => new DailyCollectionSummary
                {
                    Date = d,
                    TotalAmount = dailySummaries.FirstOrDefault(x => x.Date == d)?.TotalAmount ?? 0
                })
                .ToList();

            var totalCount = await query.CountAsync();

            // Pagination
            if (PageSize == -1)
            {
                TotalPages = 1;
                Payments = await query
                    .OrderByDescending(p => p.PaidAt)
                    .ToListAsync();
            }
            else
            {
                TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

                Payments = await query
                    .OrderByDescending(p => p.PaidAt)
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();
            }
        }

        public async Task<IActionResult> OnGetExportExcelAsync()
        {
            var query = _context.Payments
                .Where(p => p.IsPaid && !p.IsReverted)
                .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
                .Include(p => p.Movement)
                .Include(p => p.ReceiptReference)
                .Include(p => p.Collector)
                .AsQueryable();

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value.Date);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

            if (!string.IsNullOrWhiteSpace(PlateNumber))
                query = query.Where(p => p.Vehicle != null && p.Vehicle.PlateNumber.Contains(PlateNumber));

            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle != null && p.Vehicle.CarTypeId == CarTypeId.Value);

            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

            if (CollectorId.HasValue)
                query = query.Where(p => p.CollectorId == CollectorId.Value);

            var payments = await query
                .OrderByDescending(p => p.PaidAt)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Date,Plate,Owner,Mobile,Car Type,Movement,Collector,Receipt Ref,Amount");

            foreach (var p in payments)
            {
                sb.AppendLine(string.Join(",",
                    AppTime.ToLocal(p.PaidAt).ToString("yyyy-MM-dd HH:mm"),
                    p.Vehicle?.PlateNumber,
                    p.Vehicle?.OwnerName,
                    p.Vehicle?.Mobile,
                    p.Vehicle?.CarType?.Name,
                    p.Movement?.Name ?? p.MovementType,
                    p.Collector?.Username ?? "System",
                    p.ReceiptReference?.ReferenceNumber ?? "-",
                    p.Amount
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"Payments_{AppTime.Now:yyyyMMddHHmmss}.csv");
        }
    }
}
