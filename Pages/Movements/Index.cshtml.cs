using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Movements;

/// <summary>
/// Tariff list: search, active / inactive filter, paging, and activating or
/// deactivating tariffs (one row, or every ticked row).
/// </summary>
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context) => _context = context;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "asc";
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "all"; // all | active | inactive
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public List<Movement> Movements { get; set; } = new();
    public Pager Pager { get; set; } = new(0, 1, 20);
    public int ActiveCount { get; set; }
    public int InactiveCount { get; set; }

    public async Task OnGetAsync()
    {
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
        return RedirectToPage(new { Search, Sort, Status, PageSize, CurrentPage });
    }
}
