using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.CarTypes;

/// <summary>Car types as cards: vehicles, active tariffs and amount range for each.</summary>
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
    [BindProperty(SupportsGet = true)] public string Sort { get; set; } = "name"; // name | vehicles | tariffs
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 24;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public record Card(CarType Type, int Vehicles, int Tariffs, int Priced, decimal? Min, decimal? Max);

    public List<Card> Cards { get; set; } = new();
    public Pager Pager { get; set; } = new(0, 1, 24);
    public HashSet<string> My { get; set; } = new();
    public int TotalTypes { get; set; }

    public async Task OnGetAsync()
    {
        My = await _permissions.GetEffectiveAsync(User);
        TotalTypes = await _context.CarTypes.CountAsync();

        var q = _context.CarTypes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
            q = q.Where(c => EF.Functions.Like(c.Name, $"%{Search.Trim()}%"));
        var types = await q.ToListAsync();

        var vehicles = await _context.Vehicles.GroupBy(v => v.CarTypeId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var tariffs = await _context.Movements.Where(m => m.IsActive && m.CarTypeId != null)
            .GroupBy(m => m.CarTypeId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var amounts = await _context.TaxAmounts.Where(t => t.Movement != null && t.Movement.IsActive)
            .GroupBy(t => t.CarTypeId)
            .Select(g => new { g.Key, N = g.Count(), Min = g.Min(t => t.Amount), Max = g.Max(t => t.Amount) })
            .ToDictionaryAsync(x => x.Key);

        var cards = types.Select(t => new Card(t,
            vehicles.TryGetValue(t.Id, out var v) ? v : 0,
            tariffs.TryGetValue(t.Id, out var n) ? n : 0,
            amounts.TryGetValue(t.Id, out var a) ? a.N : 0,
            a?.Min, a?.Max));
        cards = Sort switch
        {
            "vehicles" => cards.OrderByDescending(c => c.Vehicles).ThenBy(c => c.Type.Name),
            "tariffs" => cards.OrderByDescending(c => c.Tariffs).ThenBy(c => c.Type.Name),
            _ => cards.OrderBy(c => c.Type.Name),
        };
        var list = cards.ToList();

        Pager = new Pager(list.Count, CurrentPage, PageSize);
        CurrentPage = Pager.Page;
        Cards = list.Skip(Pager.Skip).Take(Pager.Take).ToList();
    }
}
