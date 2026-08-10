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
    public class RevenueAccountReportItem
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = "";
        public string AccountName { get; set; } = "";
        public int TotalPayments { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class RevenueAccountReportModel : PageModel
    {
        private readonly AppDbContext _context;

        public RevenueAccountReportModel(AppDbContext context)
        {
            _context = context;
        }

        // Filters
        [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
        [BindProperty(SupportsGet = true)] public int? RevenueAccountId { get; set; }
        [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }
        [BindProperty(SupportsGet = true)] public int? CarTypeId { get; set; }

        // Select Lists
        public SelectList RevenueAccounts { get; set; } = null!;
        public SelectList Movements { get; set; } = null!;
        public SelectList CarTypes { get; set; } = null!;

        // Results
        public List<RevenueAccountReportItem> ReportItems { get; set; } = new();
        public decimal GrandTotalAmount { get; set; }
        public int GrandTotalPayments { get; set; }

        public async Task OnGetAsync()
        {
            await LoadDropdownsAsync();
            await LoadReportAsync();
        }

        public async Task<IActionResult> OnGetExportExcelAsync()
        {
            await LoadDropdownsAsync();
            await LoadReportAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Revenue Account Report");
            sb.AppendLine($"From: {FromDate?.ToString("dd-MMM-yyyy") ?? "All"}  To: {ToDate?.ToString("dd-MMM-yyyy") ?? "All"}");
            sb.AppendLine("");
            sb.AppendLine("Account Code,Account Name,Payments,Amount");

            foreach (var item in ReportItems)
            {
                sb.AppendLine($"{item.AccountCode},{item.AccountName},{item.TotalPayments},{item.TotalAmount:N0}");
            }

            sb.AppendLine($"TOTAL,{GrandTotalPayments},{GrandTotalAmount:N0}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"RevenueAccountReport_{AppTime.Now:yyyyMMddHHmmss}.csv");
        }

        private async Task LoadDropdownsAsync()
        {
            RevenueAccounts = new SelectList(
                await _context.RevenueAccounts
                    .AsNoTracking()
                    .OrderBy(r => r.AccountCode)
                    .Select(r => new { r.Id, DisplayName = $"{r.AccountCode} - {r.AccountName}" })
                    .ToListAsync(),
                "Id",
                "DisplayName"
            );

            CarTypes = new SelectList(
                await _context.CarTypes.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
                "Id",
                "Name"
            );

            Movements = new SelectList(
                await _context.Movements.AsNoTracking()
                    .GroupBy(m => m.Name)
                    .Select(g => new { Id = g.Min(x => x.Id), Name = g.Key })
                    .OrderBy(x => x.Name)
                    .ToListAsync(),
                "Id",
                "Name"
            );
        }

        private async Task LoadReportAsync()
        {
            var query = _context.Payments
                .Where(p => p.IsPaid && !p.IsReverted)
                .Include(p => p.Movement)
                    .ThenInclude(m => m.RevenueAccount)
                .Include(p => p.Vehicle).ThenInclude(v => v.CarType)
                .AsQueryable();

            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= FromDate.Value.Date);

            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt < ToDate.Value.Date.AddDays(1));

            if (RevenueAccountId.HasValue)
                query = query.Where(p => p.Movement != null && p.Movement.RevenueAccountId == RevenueAccountId.Value);

            if (MovementId.HasValue)
                query = query.Where(p => p.MovementId == MovementId.Value);

            if (CarTypeId.HasValue)
                query = query.Where(p => p.Vehicle != null && p.Vehicle.CarTypeId == CarTypeId.Value);

            ReportItems = await query
                .Where(p => p.Movement != null && p.Movement.RevenueAccount != null)
                .GroupBy(p => new
                {
                    p.Movement!.RevenueAccount!.Id,
                    p.Movement!.RevenueAccount!.AccountCode,
                    p.Movement!.RevenueAccount!.AccountName
                })
                .Select(g => new RevenueAccountReportItem
                {
                    AccountId = g.Key.Id,
                    AccountCode = g.Key.AccountCode,
                    AccountName = g.Key.AccountName,
                    TotalPayments = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount)
                })
                .OrderBy(r => r.AccountCode)
                .ToListAsync();

            GrandTotalPayments = ReportItems.Sum(r => r.TotalPayments);
            GrandTotalAmount = ReportItems.Sum(r => r.TotalAmount);
        }
    }
}
