using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Reports
{
    /// <summary>
    /// One collector: who they are, what they collected in a period and
    /// every payment they took. Opened from the Collections Dashboard.
    /// </summary>
    public class CollectorProfileModel : PageModel
    {
        private readonly AppDbContext _context;

        public CollectorProfileModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
        [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
        [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

        public User Collector { get; set; } = null!;
        public PaymentSummaryBreakdown Breakdown { get; set; } = new();
        public List<PaymentDetailRow> Payments { get; set; } = new();
        public int PendingCount { get; set; }
        public int CancelledCount { get; set; }
        public decimal TodayAmount { get; set; }
        public int TodayCount { get; set; }
        public DateTime? FirstPaymentAt { get; set; }
        public DateTime? LastPaymentAt { get; set; }

        public int PagerTotal { get; set; }
        public Pager Pager => new(PagerTotal, CurrentPage, PageSize, "CurrentPage");

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var collector = await _context.Users
                .AsNoTracking()
                .Include(u => u.Checkpoint)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (collector == null)
                return NotFound();
            Collector = collector;

            var all = _context.Payments.Where(p => p.CollectorId == id);

            // All-time figures, independent of the date filter.
            var paidAll = all.Where(p => p.IsPaid && !p.IsReverted);
            FirstPaymentAt = await paidAll.MinAsync(p => (DateTime?)p.PaidAt);
            LastPaymentAt = await paidAll.MaxAsync(p => (DateTime?)p.PaidAt);

            var today = AppTime.GetUtcDayRange(AppTime.Today);
            var paidToday = paidAll.Where(p => p.PaidAt >= today.StartUtc && p.PaidAt < today.EndUtc);
            TodayCount = await paidToday.CountAsync();
            TodayAmount = await paidToday.SumAsync(p => (decimal?)p.Amount) ?? 0m;

            // Period figures.
            var query = all;
            if (FromDate.HasValue)
                query = query.Where(p => p.PaidAt >= AppTime.ToUtc(FromDate.Value.Date));
            if (ToDate.HasValue)
                query = query.Where(p => p.PaidAt < AppTime.ToUtc(ToDate.Value.Date.AddDays(1)));

            Breakdown = await ReportDetails.SummarizeAsync(query);
            PendingCount = await query.CountAsync(p => !p.IsPaid && !p.IsReverted);
            CancelledCount = await query.CountAsync(p => p.IsReverted);

            if (PageSize <= 0) PageSize = Pager.AllRows; // "All"
            PagerTotal = await query.CountAsync();
            var pager = Pager;
            CurrentPage = pager.Page;

            var page = await pager.Apply(query
                    .Include(p => p.Vehicle).ThenInclude(v => v!.CarType)
                    .Include(p => p.Movement)
                    .Include(p => p.Collector)
                    .Include(p => p.Checkpoint)
                    .AsNoTracking()
                    .OrderByDescending(p => p.PaidAt))
                .ToListAsync();
            Payments = page.Select(ReportDetails.ToRow).ToList();

            return Page();
        }
    }
}
