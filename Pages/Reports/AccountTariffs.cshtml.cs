using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

/// <summary>
/// Each revenue account with the tariffs linked to it and the tax amount of
/// every tariff per car type. Tariffs without an account are listed last.
/// </summary>
public class AccountTariffsModel : PageModel
{
    private readonly AppDbContext _context;
    public AccountTariffsModel(AppDbContext context) => _context = context;

    /// <summary>Account id, or -1 for tariffs without an account.</summary>
    [BindProperty(SupportsGet = true)] public int? AccountId { get; set; }
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "active"; // all | active | inactive
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }

    public record Price(int MovementId, string CarType, decimal? Amount, bool IsActive);
    public record Tariff(string Name, List<Price> Prices)
    {
        public bool AnyActive => Prices.Any(p => p.IsActive);
        public int Missing => Prices.Count(p => p.Amount == null);
    }
    public record Account(int? Id, string Code, string Name, bool IsActive, List<Tariff> Tariffs)
    {
        public string Display => Id == null ? "No revenue account" : $"{Code} - {Name}";
        public int Rows => Tariffs.Sum(t => t.Prices.Count);
        public int Priced => Tariffs.Sum(t => t.Prices.Count(p => p.Amount != null));
        public decimal? Min => Tariffs.SelectMany(t => t.Prices).Min(p => p.Amount);
        public decimal? Max => Tariffs.SelectMany(t => t.Prices).Max(p => p.Amount);
    }

    public List<Account> Accounts { get; set; } = new();
    public List<RevenueAccount> AllAccounts { get; set; } = new();

    public async Task OnGetAsync()
    {
        AllAccounts = await _context.RevenueAccounts.AsNoTracking().OrderBy(a => a.AccountCode).ToListAsync();
        Accounts = await BuildAsync();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Account Code,Account Name,Tariff,Car Type,Amount,Status,System ID");
        foreach (var a in await BuildAsync())
            foreach (var t in a.Tariffs)
                foreach (var p in t.Prices)
                    sb.AppendLine(string.Join(",", Csv(a.Code), Csv(a.Id == null ? "No revenue account" : a.Name), Csv(t.Name),
                        Csv(p.CarType), p.Amount?.ToString("0.00") ?? "", p.IsActive ? "Active" : "Inactive", p.MovementId));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
            "text/csv", $"RevenueAccountTariffs_{AppTime.Now:yyyyMMdd_HHmm}.csv");
    }

    private async Task<List<Account>> BuildAsync()
    {
        var q = _context.Movements.AsNoTracking()
            .Include(m => m.CarType)
            .Include(m => m.RevenueAccount)
            .AsQueryable();

        if (AccountId == -1) q = q.Where(m => m.RevenueAccountId == null);
        else if (AccountId.HasValue) q = q.Where(m => m.RevenueAccountId == AccountId);
        if (Status == "active") q = q.Where(m => m.IsActive);
        else if (Status == "inactive") q = q.Where(m => !m.IsActive);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            q = q.Where(m => m.Name.Contains(s) || (m.CarType != null && m.CarType.Name.Contains(s)));
        }

        var movements = await q.ToListAsync();
        var ids = movements.Select(m => m.Id).ToList();
        // A tariff row's amount is the TaxAmount for its own car type.
        var amounts = (await _context.TaxAmounts.AsNoTracking()
                .Where(t => ids.Contains(t.MovementId))
                .Select(t => new { t.MovementId, t.CarTypeId, t.Amount })
                .ToListAsync())
            .GroupBy(t => (t.MovementId, t.CarTypeId))
            .ToDictionary(g => g.Key, g => g.First().Amount);

        var result = movements
            .GroupBy(m => m.RevenueAccountId)
            .Select(g =>
            {
                var ra = g.First().RevenueAccount;
                var tariffs = g.GroupBy(m => m.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(t => new Tariff(t.First().Name.Trim(), t
                        .Select(m => new Price(m.Id, m.CarType?.Name ?? "—",
                            m.CarTypeId.HasValue && amounts.TryGetValue((m.Id, m.CarTypeId.Value), out var amt) ? amt : null,
                            m.IsActive))
                        .OrderBy(p => p.CarType).ToList()))
                    .OrderBy(t => t.Name).ToList();
                return new Account(g.Key, ra?.AccountCode ?? "", ra?.AccountName ?? "", ra?.IsActive ?? true, tariffs);
            })
            .OrderBy(a => a.Id == null ? 1 : 0).ThenBy(a => a.Code)
            .ToList();

        // Show accounts with no tariffs too (unless filtering).
        if (!AccountId.HasValue && string.IsNullOrWhiteSpace(Search))
        {
            foreach (var ra in await _context.RevenueAccounts.AsNoTracking().ToListAsync())
                if (result.All(a => a.Id != ra.Id))
                    result.Add(new Account(ra.Id, ra.AccountCode, ra.AccountName, ra.IsActive, new()));
            result = result.OrderBy(a => a.Id == null ? 1 : 0).ThenBy(a => a.Code).ToList();
        }
        return result;
    }

    private static string Csv(string? v) =>
        string.IsNullOrEmpty(v) ? "" : v.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
}
