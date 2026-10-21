using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Services;

/// <summary>
/// Tariff filter for report and list pages. A tariff is stored once per car
/// type (same name, one Movement row each), so the filter lists each name
/// once and matches payments of that tariff for every car type.
/// </summary>
public static class TariffFilter
{
    public record Option(int Id, string Name);

    /// <summary>One option per tariff name; the value is the lowest id among rows with that name.</summary>
    public static Task<List<Option>> OptionsAsync(AppDbContext db) => OptionsQuery(db).ToListAsync();

    public static SelectList SelectList(AppDbContext db) =>
        new(OptionsQuery(db).ToList(), nameof(Option.Id), nameof(Option.Name));

    public static async Task<SelectList> SelectListAsync(AppDbContext db) =>
        new(await OptionsAsync(db), nameof(Option.Id), nameof(Option.Name));

    private static IQueryable<Option> OptionsQuery(AppDbContext db) =>
        db.Movements.AsNoTracking()
            .GroupBy(m => m.Name)
            .OrderBy(g => g.Key)
            .Select(g => new Option(g.Min(x => x.Id), g.Key));

    /// <summary>Payments whose tariff has the same name as the chosen one (any car type).</summary>
    public static IQueryable<Payment> Apply(IQueryable<Payment> query, AppDbContext db, int? movementId)
    {
        if (!movementId.HasValue) return query;
        var name = db.Movements.Where(m => m.Id == movementId.Value).Select(m => m.Name);
        return query.Where(p => p.Movement != null && name.Contains(p.Movement.Name));
    }
}
