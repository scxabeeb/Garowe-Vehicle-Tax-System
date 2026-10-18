using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Finance;

/// <summary>
/// Finance dashboard for one day: money collected (paid payments only),
/// by revenue account, and how much of it is already in an RV and sent to
/// FMIS. Access follows the Finance permissions (PagePermissionFilter).
/// </summary>
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    public IndexModel(AppDbContext context) => _context = context;

    /// <summary>Single day (older links); same as FromDate = ToDate = Date.</summary>
    [BindProperty(SupportsGet = true)] public DateTime? Date { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    /// <summary>Where a paid payment is in the finance process.</summary>
    public enum Step { NeedsRv, InRv, Sent, Failed }

    public record PaymentRow(Payment Payment, string? Account, Step Step, string? RvNumber);

    public record AccountRow(int? AccountId, string Account, int Payments, decimal Total, int Waiting, decimal WaitingAmount)
    {
        public bool Done => Waiting == 0;
    }

    public List<AccountRow> Accounts { get; set; } = new();
    public List<PaymentRow> Rows { get; set; } = new();
    public Pager Pager { get; set; } = new(0, 1, 20);

    public int Collected { get; set; }
    public decimal CollectedAmount { get; set; }
    public int InRv { get; set; }
    public decimal InRvAmount { get; set; }
    public int Sent { get; set; }
    public decimal SentAmount { get; set; }
    public int NeedsRv => Collected - InRv;
    public decimal NeedsRvAmount => CollectedAmount - InRvAmount;
    public int Failed { get; set; }

    public bool IsSingleDay => FromDate == ToDate;
    public bool IsToday => IsSingleDay && FromDate == AppTime.Today;

    public async Task OnGetAsync()
    {
        // Period in Somalia days: From–To, a single Date, or today.
        FromDate = (FromDate ?? Date ?? ToDate ?? AppTime.Today).Date;
        ToDate = (ToDate ?? Date ?? FromDate).Value.Date;
        if (ToDate < FromDate) (FromDate, ToDate) = (ToDate, FromDate);
        Date = FromDate;
        var start = AppTime.GetUtcDayRange(FromDate.Value).StartUtc;
        var end = AppTime.GetUtcDayRange(ToDate.Value).EndUtc;

        // Only money actually collected: paid and not cancelled.
        var payments = await _context.Payments.AsNoTracking()
            .Include(p => p.Vehicle)
            .Include(p => p.Collector)
            .Include(p => p.Movement).ThenInclude(m => m!.RevenueAccount)
            .Where(p => p.IsPaid && !p.IsReverted && p.PaidAt >= start && p.PaidAt < end)
            .OrderByDescending(p => p.PaidAt).ThenByDescending(p => p.Id)
            .ToListAsync();

        var ids = payments.Select(p => p.Id).ToList();
        var rvs = (await _context.RvPayments.AsNoTracking()
                .Where(r => ids.Contains(r.PaymentId) && r.RvDocument != null && r.RvDocument.Status != RvStatus.Cancelled)
                .Select(r => new { r.PaymentId, r.RvDocument!.RvNumber, r.RvDocument.Status, r.RvDocument.FmisStatus })
                .ToListAsync())
            .GroupBy(r => r.PaymentId).ToDictionary(g => g.Key, g => g.First());

        var all = payments.Select(p =>
        {
            var account = p.Movement?.RevenueAccount?.DisplayName;
            if (!rvs.TryGetValue(p.Id, out var rv)) return new PaymentRow(p, account, Step.NeedsRv, null);
            var stage = RvStages.Of(rv.Status, rv.FmisStatus);
            var step = stage == RvStage.Sent ? Step.Sent : stage == RvStage.Failed ? Step.Failed : Step.InRv;
            return new PaymentRow(p, account, step, rv.RvNumber);
        }).ToList();

        Collected = all.Count;
        CollectedAmount = all.Sum(r => r.Payment.Amount);
        InRv = all.Count(r => r.Step != Step.NeedsRv);
        InRvAmount = all.Where(r => r.Step != Step.NeedsRv).Sum(r => r.Payment.Amount);
        Sent = all.Count(r => r.Step == Step.Sent);
        SentAmount = all.Where(r => r.Step == Step.Sent).Sum(r => r.Payment.Amount);
        Failed = all.Count(r => r.Step == Step.Failed);

        Accounts = all
            .GroupBy(r => r.Payment.Movement?.RevenueAccountId)
            .Select(g => new AccountRow(g.Key, g.First().Account ?? "No revenue account", g.Count(), g.Sum(r => r.Payment.Amount),
                g.Count(r => r.Step == Step.NeedsRv), g.Where(r => r.Step == Step.NeedsRv).Sum(r => r.Payment.Amount)))
            .OrderByDescending(a => a.Total)
            .ToList();

        Pager = new Pager(all.Count, CurrentPage, PageSize);
        CurrentPage = Pager.Page;
        Rows = all.Skip(Pager.Skip).Take(Pager.Take).ToList();
    }
}
