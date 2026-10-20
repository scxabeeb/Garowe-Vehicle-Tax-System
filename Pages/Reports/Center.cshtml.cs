using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Reports;

/// <summary>
/// Reports Center: today's key figures and every report, grouped, with what
/// each one answers (Services/ReportCatalog).
/// </summary>
public class CenterModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IPermissionService _permissions;

    public CenterModel(AppDbContext context, IPermissionService permissions)
    {
        _context = context;
        _permissions = permissions;
    }

    public HashSet<string> My { get; set; } = new();
    public decimal PaidToday { get; set; }
    public int PaidTodayCount { get; set; }
    public decimal PendingToday { get; set; }
    public int PendingTodayCount { get; set; }
    public int CancelledToday { get; set; }
    public decimal MonthTotal { get; set; }

    public async Task OnGetAsync()
    {
        My = await _permissions.GetEffectiveAsync(User);
        if (!My.Contains("Reports.View") && !My.Contains("Finance.View")) return;

        var (dayStart, dayEnd) = AppTime.GetUtcDayRange(AppTime.Today);
        var monthStart = AppTime.ToUtc(new DateTime(AppTime.Today.Year, AppTime.Today.Month, 1));
        var today = await _context.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= dayStart && p.PaidAt < dayEnd)
            .GroupBy(p => p.IsReverted ? 2 : p.IsPaid ? 0 : 1)
            .Select(g => new { g.Key, N = g.Count(), Sum = g.Sum(p => p.Amount) })
            .ToListAsync();
        (PaidTodayCount, PaidToday) = today.Where(t => t.Key == 0).Select(t => (t.N, t.Sum)).FirstOrDefault();
        (PendingTodayCount, PendingToday) = today.Where(t => t.Key == 1).Select(t => (t.N, t.Sum)).FirstOrDefault();
        CancelledToday = today.Where(t => t.Key == 2).Sum(t => t.N);
        MonthTotal = await _context.Payments.AsNoTracking()
            .Where(p => p.IsPaid && !p.IsReverted && p.PaidAt >= monthStart && p.PaidAt < dayEnd)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
    }
}
