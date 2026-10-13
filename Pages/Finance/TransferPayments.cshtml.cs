using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Finance;

/// <summary>
/// Admins move payments collected under the wrong Traffida to the right one,
/// which moves them to that Traffida's revenue account. The amount is kept;
/// payments in an active RV are locked (cancel the RV first).
/// </summary>
public class TransferPaymentsModel : PageModel
{
    private readonly AppDbContext _context;

    public TransferPaymentsModel(AppDbContext context) => _context = context;

    // Search
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public int? AccountId { get; set; }
    [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    // Transfer
    [BindProperty] public List<int> PaymentIds { get; set; } = new();
    [BindProperty] public int? TargetMovementId { get; set; }
    [BindProperty] public string? Reason { get; set; }

    public record Row(Payment Payment, string? Account, string? LockedBy);
    public record MovementOption(int Id, string Name, string? CarType, int? AccountId, string Account);

    public List<Row> Rows { get; set; } = new();
    public Pager Pager { get; set; } = new(0, 1, 20);
    public List<MovementOption> Movements { get; set; } = new();
    public List<RevenueAccount> Accounts { get; set; } = new();
    public List<PaymentTransfer> History { get; set; } = new();
    public bool Searched => !string.IsNullOrWhiteSpace(Search) || FromDate.HasValue || ToDate.HasValue || AccountId.HasValue || MovementId.HasValue;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await IsAdminAsync()) return Forbid();
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostTransferAsync()
    {
        if (!await IsAdminAsync()) return Forbid();

        var reason = (Reason ?? "").Trim();
        var ids = PaymentIds.Distinct().ToList();
        var target = TargetMovementId.HasValue
            ? await _context.Movements.Include(m => m.RevenueAccount).FirstOrDefaultAsync(m => m.Id == TargetMovementId.Value)
            : null;

        string? error =
            ids.Count == 0 ? "Select at least one payment." :
            target == null ? "Choose the correct Traffida." :
            reason.Length < 5 ? "Enter the reason for the transfer (at least 5 characters)." :
            null;
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToPage(RouteValues());
        }

        var payments = await _context.Payments
            .Include(p => p.Movement).ThenInclude(m => m!.RevenueAccount)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();
        var locked = await LockedAsync(ids);
        var me = await CurrentUserAsync();

        int moved = 0;
        var skipped = new List<string>();
        await using var tx = await _context.Database.BeginTransactionAsync();
        foreach (var p in payments)
        {
            if (p.IsReverted) { skipped.Add($"#{p.Id} (cancelled)"); continue; }
            if (locked.TryGetValue(p.Id, out var rv)) { skipped.Add($"#{p.Id} (in RV {rv})"); continue; }
            if (p.MovementId == target!.Id) { skipped.Add($"#{p.Id} (already there)"); continue; }

            _context.PaymentTransfers.Add(new PaymentTransfer
            {
                PaymentId = p.Id,
                FromMovementId = p.MovementId,
                FromMovement = Cut(p.Movement?.Name ?? p.MovementType, 100),
                FromAccount = Cut(p.Movement?.RevenueAccount?.DisplayName, 150),
                ToMovementId = target.Id,
                ToMovement = Cut(target.Name, 100),
                ToAccount = Cut(target.RevenueAccount?.DisplayName, 150),
                Amount = p.Amount,
                Reason = Cut(reason, 500)!,
                ByUserId = me?.Id,
                ByUsername = Cut(me?.Username ?? User.Identity?.Name ?? "", 100)!,
            });

            // The amount collected stays exactly the same.
            p.MovementId = target.Id;
            p.MovementType = target.Name;
            moved++;
        }
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        if (moved > 0)
            TempData["Message"] = $"{moved} payment(s) transferred to {target!.Name} → {target.RevenueAccount?.DisplayName ?? "no revenue account"}.";
        if (skipped.Count > 0)
            TempData["Error"] = "Not transferred: " + string.Join(", ", skipped.Take(20)) + (skipped.Count > 20 ? " …" : "");
        return RedirectToPage(RouteValues());
    }

    private object RouteValues() => new
    {
        Search, FromDate = FromDate?.ToString("yyyy-MM-dd"), ToDate = ToDate?.ToString("yyyy-MM-dd"),
        AccountId, MovementId, PageSize, CurrentPage,
    };

    private async Task LoadAsync()
    {
        Accounts = await _context.RevenueAccounts.AsNoTracking().OrderBy(a => a.AccountCode).ToListAsync();
        Movements = (await _context.Movements.AsNoTracking()
                .Include(m => m.RevenueAccount).Include(m => m.CarType)
                .ToListAsync())
            .Select(m => new MovementOption(m.Id, m.Name, m.CarType?.Name, m.RevenueAccountId,
                m.RevenueAccount?.DisplayName ?? "—"))
            .OrderBy(m => m.Account).ThenBy(m => m.Name).ThenBy(m => m.CarType)
            .ToList();

        History = await _context.PaymentTransfers.AsNoTracking()
            .OrderByDescending(t => t.At).ThenByDescending(t => t.Id)
            .Take(15)
            .ToListAsync();

        if (!Searched) return;

        var q = _context.Payments.AsNoTracking()
            .Include(p => p.Vehicle)
            .Include(p => p.Collector)
            .Include(p => p.Movement).ThenInclude(m => m!.RevenueAccount)
            .Where(p => !p.IsReverted);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            // System ID, invoice number (yyyyMM + ID), plate, ref no or Golis bill.
            int? id = int.TryParse(s, out var n) ? n : null;
            int? fromInvoice = s.Length > 6 && long.TryParse(s, out _) && int.TryParse(s[6..], out var inv) ? inv : null;
            q = q.Where(p => (id != null && (p.Id == id || p.ReferenceNo == id)) ||
                             (fromInvoice != null && p.Id == fromInvoice) ||
                             (p.Vehicle != null && p.Vehicle.PlateNumber.Contains(s)) ||
                             (p.TransactionId != null && p.TransactionId == s));
        }
        if (FromDate.HasValue)
        {
            var from = AppTime.ToUtc(FromDate.Value.Date);
            q = q.Where(p => p.PaidAt >= from);
        }
        if (ToDate.HasValue)
        {
            var to = AppTime.ToUtc(ToDate.Value.Date.AddDays(1));
            q = q.Where(p => p.PaidAt < to);
        }
        if (AccountId.HasValue) q = q.Where(p => p.Movement != null && p.Movement.RevenueAccountId == AccountId);
        if (MovementId.HasValue) q = q.Where(p => p.MovementId == MovementId);

        Pager = new Pager(await q.CountAsync(), CurrentPage, PageSize);
        CurrentPage = Pager.Page;
        var page = await Pager.Apply(q.OrderByDescending(p => p.PaidAt).ThenByDescending(p => p.Id)).ToListAsync();
        var locked = await LockedAsync(page.Select(p => p.Id).ToList());
        Rows = page.Select(p => new Row(p, p.Movement?.RevenueAccount?.DisplayName,
            locked.TryGetValue(p.Id, out var rv) ? rv : null)).ToList();
    }

    /// <summary>Payments in an RV that is not cancelled → that RV's number.</summary>
    private async Task<Dictionary<int, string>> LockedAsync(List<int> ids) =>
        (await _context.RvPayments.AsNoTracking()
            .Where(r => ids.Contains(r.PaymentId) && r.RvDocument != null && r.RvDocument.Status != RvStatus.Cancelled)
            .Select(r => new { r.PaymentId, r.RvDocument!.RvNumber })
            .ToListAsync())
        .GroupBy(x => x.PaymentId)
        .ToDictionary(g => g.Key, g => g.First().RvNumber);

    private Task<User?> CurrentUserAsync()
    {
        var name = User.Identity?.Name;
        return name == null ? Task.FromResult<User?>(null) : _context.Users.FirstOrDefaultAsync(u => u.Username == name);
    }

    /// <summary>Admins only, checked against the database (not the login cookie).</summary>
    private async Task<bool> IsAdminAsync()
    {
        if (User.IsInRole(AppPermissions.SuperAdminRole)) return true;
        var me = await CurrentUserAsync();
        return me != null && !me.IsLocked &&
               (string.Equals(me.Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(me.Role, AppPermissions.SuperAdminRole, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Cut(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;
}
