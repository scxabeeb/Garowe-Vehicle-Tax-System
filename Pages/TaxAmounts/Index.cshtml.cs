using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.TaxAmounts;

/// <summary>
/// Tax amounts with filters, and the active tariffs that still have no
/// amount for their car type ("Missing amounts").
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
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "all"; // all | active | inactive (tariff)
    [BindProperty(SupportsGet = true)] public string Show { get; set; } = "amounts"; // amounts | missing
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public List<TaxAmount> Taxes { get; set; } = new();
    public List<Movement> Missing { get; set; } = new();
    public List<CarType> CarTypes { get; set; } = new();
    public List<RevenueAccount> Accounts { get; set; } = new();
    public Pager Pager { get; set; } = new(0, 1, 20);
    public HashSet<string> My { get; set; } = new();
    public int AmountCount { get; set; }
    public int MissingCount { get; set; }

    public async Task OnGetAsync()
    {
        My = await _permissions.GetEffectiveAsync(User);
        CarTypes = await _context.CarTypes.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        Accounts = await _context.RevenueAccounts.AsNoTracking().OrderBy(a => a.AccountCode).ToListAsync();
        var s = Search?.Trim();

        // Amounts
        var taxes = _context.TaxAmounts.AsNoTracking()
            .Include(t => t.CarType)
            .Include(t => t.Movement).ThenInclude(m => m!.RevenueAccount)
            .AsQueryable();
        if (!string.IsNullOrEmpty(s))
            taxes = taxes.Where(t => t.CarType!.Name.Contains(s) || t.Movement!.Name.Contains(s));
        if (CarTypeId.HasValue) taxes = taxes.Where(t => t.CarTypeId == CarTypeId);
        if (AccountId == -1) taxes = taxes.Where(t => t.Movement!.RevenueAccountId == null);
        else if (AccountId.HasValue) taxes = taxes.Where(t => t.Movement!.RevenueAccountId == AccountId);
        if (Status == "active") taxes = taxes.Where(t => t.Movement!.IsActive);
        else if (Status == "inactive") taxes = taxes.Where(t => !t.Movement!.IsActive);

        // Active tariff rows with no amount for their own car type
        var missing = _context.Movements.AsNoTracking()
            .Include(m => m.CarType).Include(m => m.RevenueAccount)
            .Where(m => m.IsActive && m.CarTypeId != null &&
                        !_context.TaxAmounts.Any(t => t.MovementId == m.Id && t.CarTypeId == m.CarTypeId));
        if (!string.IsNullOrEmpty(s))
            missing = missing.Where(m => m.Name.Contains(s) || m.CarType!.Name.Contains(s));
        if (CarTypeId.HasValue) missing = missing.Where(m => m.CarTypeId == CarTypeId);
        if (AccountId == -1) missing = missing.Where(m => m.RevenueAccountId == null);
        else if (AccountId.HasValue) missing = missing.Where(m => m.RevenueAccountId == AccountId);

        AmountCount = await taxes.CountAsync();
        MissingCount = await missing.CountAsync();

        if (Show == "missing")
        {
            Pager = new Pager(MissingCount, CurrentPage, PageSize);
            Missing = await Pager.Apply(missing.OrderBy(m => m.Name).ThenBy(m => m.CarType!.Name)).ToListAsync();
        }
        else
        {
            Pager = new Pager(AmountCount, CurrentPage, PageSize);
            Taxes = await Pager.Apply(taxes.OrderBy(t => t.Movement!.Name).ThenBy(t => t.CarType!.Name)).ToListAsync();
        }
        CurrentPage = Pager.Page;
    }
}
