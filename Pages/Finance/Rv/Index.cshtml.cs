using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Finance.Rv;

/// <summary>
/// RV History: every RV document with one plain status (being prepared,
/// ready to send, sent to FMIS, sending failed, cancelled). Access follows
/// the Finance permissions (PagePermissionFilter).
/// </summary>
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    public IndexModel(AppDbContext context) => _context = context;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public int? AccountId { get; set; }
    [BindProperty(SupportsGet = true)] public RvStage? Stage { get; set; }
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public record Row(RvDocument Rv, RvStage Stage);

    public List<Row> Rows { get; set; } = new();
    public List<RevenueAccount> Accounts { get; set; } = new();
    public Pager Pager { get; set; } = new(0, 1, 20);

    /// <summary>Count and amount per stage, for the chips (before the stage filter).</summary>
    public Dictionary<RvStage, (int Count, decimal Amount)> ByStage { get; set; } = new();
    public int Total => ByStage.Values.Sum(v => v.Count);
    public decimal FilteredAmount { get; set; }

    public async Task OnGetAsync()
    {
        Accounts = await _context.RevenueAccounts.AsNoTracking().OrderBy(a => a.AccountCode).ToListAsync();

        var q = _context.RvDocuments.AsNoTracking()
            .Include(r => r.RevenueAccount)
            .Include(r => r.PreparedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            q = q.Where(r => r.RvNumber.Contains(s) || (r.FmisBatchNumber != null && r.FmisBatchNumber.Contains(s)));
        }
        // RvDate holds the local day the RV was made.
        if (FromDate.HasValue) q = q.Where(r => r.RvDate >= FromDate.Value.Date);
        if (ToDate.HasValue) q = q.Where(r => r.RvDate < ToDate.Value.Date.AddDays(1));
        if (AccountId.HasValue) q = q.Where(r => r.RevenueAccountId == AccountId);

        // RV counts are small; work out each one's plain stage in memory.
        var all = (await q.OrderByDescending(r => r.RvDate).ThenByDescending(r => r.Id).ToListAsync())
            .Select(r => new Row(r, RvStages.Of(r.Status, r.FmisStatus)))
            .ToList();

        ByStage = all.GroupBy(r => r.Stage)
            .ToDictionary(g => g.Key, g => (g.Count(), g.Sum(r => r.Rv.TotalAmount)));

        if (Stage.HasValue) all = all.Where(r => r.Stage == Stage).ToList();
        FilteredAmount = all.Where(r => r.Stage != RvStage.Cancelled).Sum(r => r.Rv.TotalAmount);

        Pager = new Pager(all.Count, CurrentPage, PageSize);
        CurrentPage = Pager.Page;
        Rows = all.Skip(Pager.Skip).Take(Pager.Take).ToList();
    }
}
