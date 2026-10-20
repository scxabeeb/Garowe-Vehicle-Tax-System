using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Reports
{
    /// <summary>
    /// Tariff Types report: every tariff row (one per car type) with its
    /// revenue account, tax amount and status.
    /// </summary>
    public class MovementsModel : PageModel
    {
        private readonly AppDbContext _context;

        public MovementsModel(AppDbContext context) => _context = context;

        [BindProperty(SupportsGet = true)] public string? MovementName { get; set; }
        [BindProperty(SupportsGet = true)] public int? CarTypeId { get; set; }
        /// <summary>Revenue account id, or -1 for tariffs without one.</summary>
        [BindProperty(SupportsGet = true)] public int? AccountId { get; set; }
        [BindProperty(SupportsGet = true)] public string Status { get; set; } = "all"; // all | active | inactive
        /// <summary>Only tariffs collected at this checkpoint (paid payments), with their counts.</summary>
        [BindProperty(SupportsGet = true)] public int? CheckpointId { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
        [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
        [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;

        public Pager Pager => new(TotalRecords, PageNumber, PageSize, "PageNumber");

        public record Row(Movement Tariff, decimal? Amount, int Payments, decimal Collected);

        public List<Row> Rows { get; set; } = new();
        public List<CarType> CarTypes { get; set; } = new();
        public List<RevenueAccount> Accounts { get; set; } = new();
        public List<Checkpoint> Checkpoints { get; set; } = new();
        /// <summary>A checkpoint or date is chosen: rows are the tariffs collected there / then.</summary>
        public bool ByCollection => CheckpointId.HasValue || FromDate.HasValue || ToDate.HasValue;
        public int PaymentCount { get; set; }
        public decimal CollectedTotal { get; set; }
        public List<CountGroup> SummaryGroups { get; set; } = new();

        public int TotalRecords { get; set; }
        public int TariffNames { get; set; }
        public int ActiveCount { get; set; }
        public int NoAccountCount { get; set; }
        public int NoAmountCount { get; set; }

        public async Task OnGetAsync()
        {
            if (PageSize <= 0) PageSize = Pager.AllRows; // "All"
            CarTypes = await _context.CarTypes.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            Accounts = await _context.RevenueAccounts.AsNoTracking().OrderBy(a => a.AccountCode).ToListAsync();
            Checkpoints = await _context.Checkpoints.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

            var all = await LoadAsync();
            TotalRecords = all.Count;
            TariffNames = all.Select(r => r.Tariff.Name.Trim().ToLower()).Distinct().Count();
            ActiveCount = all.Count(r => r.Tariff.IsActive);
            NoAccountCount = all.Count(r => r.Tariff.RevenueAccountId == null);
            NoAmountCount = all.Count(r => r.Amount == null);
            PaymentCount = all.Sum(r => r.Payments);
            CollectedTotal = all.Sum(r => r.Collected);

            SummaryGroups = new()
            {
                new("By Revenue Account", "bi-wallet2",
                    ReportDetails.CountBy(all.Select(r => r.Tariff.RevenueAccount?.DisplayName ?? "No revenue account"), n => n)),
                new("By Car Type", "bi-truck",
                    ReportDetails.CountBy(all.Select(r => r.Tariff.CarType?.Name), n => n)),
                new("By Status", "bi-toggle-on",
                    ReportDetails.CountBy(all.Select(r => r.Tariff.IsActive ? "Active" : "Inactive"), n => n)),
            };

            var p = Pager;
            Rows = all.Skip(p.Skip).Take(p.Take).ToList();
        }

        public async Task<IActionResult> OnGetExportCsvAsync()
        {
            var sb = new StringBuilder();
            sb.AppendLine("System ID,Tariff,Car Type,Account Code,Account Name,Tax Amount,Status,Payments,Collected");
            foreach (var r in await LoadAsync())
                sb.AppendLine(string.Join(",", r.Tariff.Id, Csv(r.Tariff.Name), Csv(r.Tariff.CarType?.Name),
                    Csv(r.Tariff.RevenueAccount?.AccountCode), Csv(r.Tariff.RevenueAccount?.AccountName),
                    VehicleTax.Web.Services.Money.Csv(r.Amount), r.Tariff.IsActive ? "Active" : "Inactive",
                    r.Payments, VehicleTax.Web.Services.Money.Csv(r.Collected)));
            return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
                "text/csv", $"TariffTypes_{AppTime.Now:yyyyMMdd_HHmm}.csv");
        }

        private async Task<List<Row>> LoadAsync()
        {
            var q = _context.Movements.AsNoTracking()
                .Include(m => m.CarType)
                .Include(m => m.RevenueAccount)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(MovementName)) q = q.Where(m => m.Name.Contains(MovementName.Trim()));
            if (CarTypeId.HasValue) q = q.Where(m => m.CarTypeId == CarTypeId);
            if (AccountId == -1) q = q.Where(m => m.RevenueAccountId == null);
            else if (AccountId.HasValue) q = q.Where(m => m.RevenueAccountId == AccountId);
            if (Status == "active") q = q.Where(m => m.IsActive);
            else if (Status == "inactive") q = q.Where(m => !m.IsActive);

            var tariffs = await q.OrderBy(m => m.Name).ThenBy(m => m.CarType!.Name).ToListAsync();
            var ids = tariffs.Select(m => m.Id).ToList();
            // A tariff row's amount is the tax amount for its own car type.
            var amounts = (await _context.TaxAmounts.AsNoTracking()
                    .Where(t => ids.Contains(t.MovementId))
                    .Select(t => new { t.MovementId, t.CarTypeId, t.Amount })
                    .ToListAsync())
                .GroupBy(t => (t.MovementId, t.CarTypeId))
                .ToDictionary(g => g.Key, g => g.First().Amount);

            // Paid payments per tariff (at the chosen checkpoint / dates).
            var paid = _context.Payments.AsNoTracking().Where(p => p.IsPaid && !p.IsReverted && ids.Contains(p.MovementId));
            if (CheckpointId.HasValue) paid = paid.Where(p => p.CheckpointId == CheckpointId);
            if (FromDate.HasValue)
            {
                var from = AppTime.ToUtc(FromDate.Value.Date);
                paid = paid.Where(p => p.PaidAt >= from);
            }
            if (ToDate.HasValue)
            {
                var to = AppTime.ToUtc(ToDate.Value.Date.AddDays(1));
                paid = paid.Where(p => p.PaidAt < to);
            }
            var stats = await paid.GroupBy(p => p.MovementId)
                .Select(g => new { g.Key, N = g.Count(), Sum = g.Sum(p => p.Amount) })
                .ToDictionaryAsync(x => x.Key);

            var rows = tariffs.Select(m => new Row(m,
                m.CarTypeId.HasValue && amounts.TryGetValue((m.Id, m.CarTypeId.Value), out var a) ? a : null,
                stats.TryGetValue(m.Id, out var st) ? st.N : 0,
                stats.TryGetValue(m.Id, out var st2) ? st2.Sum : 0m));
            // With a checkpoint or dates chosen, show only the tariffs collected there.
            if (ByCollection) rows = rows.Where(r => r.Payments > 0).OrderByDescending(r => r.Collected);
            return rows.ToList();
        }

        private static string Csv(string? v) =>
            string.IsNullOrEmpty(v) ? "" : v.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
