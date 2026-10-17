using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Movements;

/// <summary>
/// Tariff list: search, car type / account / status filters, tax amount per
/// row, paging, and activating or deactivating tariffs (one row or ticked rows).
/// </summary>
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IPermissionService _permissions;

    public IndexModel(AppDbContext context, IPermissionService permissions)
    {
        _context = context;
        _permissions = permissions;
    }

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int? CarTypeId { get; set; }
    /// <summary>Revenue account id, or -1 for tariffs without one.</summary>
    [BindProperty(SupportsGet = true)] public int? AccountId { get; set; }
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "asc";
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "all"; // all | active | inactive
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public List<Movement> Movements { get; set; } = new();
    public Dictionary<int, decimal> Amounts { get; set; } = new();
    public List<CarType> CarTypes { get; set; } = new();
    public List<RevenueAccount> Accounts { get; set; } = new();
    public Pager Pager { get; set; } = new(0, 1, 20);
    public HashSet<string> My { get; set; } = new();
    public int ActiveCount { get; set; }
    public int InactiveCount { get; set; }

    public async Task OnGetAsync()
    {
        My = await _permissions.GetEffectiveAsync(User);
        CarTypes = await _context.CarTypes.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        Accounts = await _context.RevenueAccounts.AsNoTracking().OrderBy(a => a.AccountCode).ToListAsync();

        IQueryable<Movement> query = _context.Movements.AsNoTracking()
            .Include(m => m.CarType)
            .Include(m => m.RevenueAccount);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            query = query.Where(m => EF.Functions.Like(m.Name, $"%{s}%") ||
                                     (m.CarType != null && EF.Functions.Like(m.CarType.Name, $"%{s}%")) ||
                                     (m.RevenueAccount != null && EF.Functions.Like(m.RevenueAccount.AccountName, $"%{s}%")));
        }
        if (CarTypeId.HasValue) query = query.Where(m => m.CarTypeId == CarTypeId);
        if (AccountId == -1) query = query.Where(m => m.RevenueAccountId == null);
        else if (AccountId.HasValue) query = query.Where(m => m.RevenueAccountId == AccountId);

        ActiveCount = await query.CountAsync(m => m.IsActive);
        InactiveCount = await query.CountAsync(m => !m.IsActive);

        query = Status switch
        {
            "active" => query.Where(m => m.IsActive),
            "inactive" => query.Where(m => !m.IsActive),
            _ => query,
        };
        query = Sort == "desc"
            ? query.OrderByDescending(m => m.Name).ThenBy(m => m.CarType!.Name)
            : query.OrderBy(m => m.Name).ThenBy(m => m.CarType!.Name);

        var total = Status == "active" ? ActiveCount : Status == "inactive" ? InactiveCount : ActiveCount + InactiveCount;
        Pager = new Pager(total, CurrentPage, PageSize);
        CurrentPage = Pager.Page;
        Movements = await Pager.Apply(query).ToListAsync();

        // Each row's tax amount is the one for its own car type.
        var ids = Movements.Select(m => m.Id).ToList();
        var taxes = await _context.TaxAmounts.AsNoTracking()
            .Where(t => ids.Contains(t.MovementId))
            .Select(t => new { t.MovementId, t.CarTypeId, t.Amount })
            .ToListAsync();
        foreach (var m in Movements)
        {
            var t = taxes.FirstOrDefault(x => x.MovementId == m.Id && x.CarTypeId == m.CarTypeId);
            if (t != null) Amounts[m.Id] = t.Amount;
        }
    }

    /// <summary>Activate or deactivate the given tariffs.</summary>
    public async Task<IActionResult> OnPostSetActiveAsync(List<int> ids, bool active)
    {
        ids = ids.Distinct().ToList();
        if (ids.Count == 0)
        {
            TempData["Error"] = "Tick at least one tariff.";
        }
        else
        {
            var rows = await _context.Movements.Where(m => ids.Contains(m.Id) && m.IsActive != active).ToListAsync();
            foreach (var m in rows) m.IsActive = active;
            await _context.SaveChangesAsync();
            TempData["Success"] = active
                ? $"{rows.Count} tariff(s) activated."
                : $"{rows.Count} tariff(s) deactivated. They no longer appear when collecting tax.";
        }
        return RedirectToPage(new { Search, CarTypeId, AccountId, Sort, Status, PageSize, CurrentPage });
    }
}
