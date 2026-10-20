using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Vehicles;

/// <summary>Vehicle list: search across all vehicles, filters, payment status, paging.</summary>
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
    [BindProperty(SupportsGet = true)] public string Paid { get; set; } = "all"; // all | today | nottoday | never
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "plate"; // plate | newest | owner
    [BindProperty(SupportsGet = true)] public string? State { get; set; } // active | inactive
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public record Row(Vehicle Vehicle, int Payments, DateTime? LastPaid);

    public List<Row> Rows { get; set; } = new();
    public List<CarType> CarTypes { get; set; } = new();
    public Pager Pager { get; set; } = new(0, 1, 20);
    public HashSet<string> My { get; set; } = new();

    public int Total { get; set; }
    public int PaidTodayCount { get; set; }
    public int NeverPaidCount { get; set; }
    public int InactiveCount { get; set; }

    /// <summary>Switch a vehicle between Active and Inactive.</summary>
    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var v = await _context.Vehicles.FirstOrDefaultAsync(x => x.Id == id);
        if (v != null)
        {
            v.IsActive = !v.IsActive;
            await _context.SaveChangesAsync();
            TempData["Message"] = v.IsActive
                ? $"Vehicle {v.PlateNumber} is now active."
                : $"Vehicle {v.PlateNumber} is now inactive. It cannot get new invoices or payments.";
        }
        return RedirectToPage(new { Search, CarTypeId, Paid, Sort, State, PageSize, CurrentPage });
    }

    public async Task OnGetAsync()
    {
        My = await _permissions.GetEffectiveAsync(User);
        CarTypes = await _context.CarTypes.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        var (dayStart, dayEnd) = AppTime.GetUtcDayRange(AppTime.Today);

        var q = _context.Vehicles.AsNoTracking().Include(v => v.CarType).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            var digits = new string(s.Where(char.IsDigit).ToArray());
            q = q.Where(v => v.PlateNumber.Contains(s) || v.OwnerName.Contains(s) ||
                             (digits.Length >= 3 && v.Mobile.Contains(digits)));
        }
        if (CarTypeId.HasValue) q = q.Where(v => v.CarTypeId == CarTypeId);
        InactiveCount = await q.CountAsync(v => !v.IsActive);
        if (State == "active") q = q.Where(v => v.IsActive);
        else if (State == "inactive") q = q.Where(v => !v.IsActive);

        // Counts for the chips (within search / car type).
        var paid = _context.Payments.Where(p => p.IsPaid && !p.IsReverted);
        Total = await q.CountAsync();
        PaidTodayCount = await q.CountAsync(v => paid.Any(p => p.VehicleId == v.Id && p.PaidAt >= dayStart && p.PaidAt < dayEnd));
        NeverPaidCount = await q.CountAsync(v => !paid.Any(p => p.VehicleId == v.Id));

        q = Paid switch
        {
            "today" => q.Where(v => paid.Any(p => p.VehicleId == v.Id && p.PaidAt >= dayStart && p.PaidAt < dayEnd)),
            "nottoday" => q.Where(v => !paid.Any(p => p.VehicleId == v.Id && p.PaidAt >= dayStart && p.PaidAt < dayEnd)),
            "never" => q.Where(v => !paid.Any(p => p.VehicleId == v.Id)),
            _ => q,
        };
        var count = Paid switch
        {
            "today" => PaidTodayCount,
            "nottoday" => Total - PaidTodayCount,
            "never" => NeverPaidCount,
            _ => Total,
        };

        q = Sort switch
        {
            "newest" => q.OrderByDescending(v => v.Id),
            "owner" => q.OrderBy(v => v.OwnerName).ThenBy(v => v.PlateNumber),
            _ => q.OrderBy(v => v.PlateNumber),
        };

        Pager = new Pager(count, CurrentPage, PageSize);
        CurrentPage = Pager.Page;
        var page = await Pager.Apply(q).ToListAsync();

        var ids = page.Select(v => v.Id).ToList();
        var stats = await paid.Where(p => ids.Contains(p.VehicleId))
            .GroupBy(p => p.VehicleId)
            .Select(g => new { g.Key, N = g.Count(), Last = g.Max(p => p.PaidAt) })
            .ToDictionaryAsync(x => x.Key);
        Rows = page.Select(v => stats.TryGetValue(v.Id, out var st)
            ? new Row(v, st.N, st.Last) : new Row(v, 0, null)).ToList();
    }
}
