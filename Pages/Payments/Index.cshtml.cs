using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Payments;

/// <summary>
/// Payments List: search every payment and act on it (open / print the
/// receipt, transfer to another tariff, cancel). Collectors see only the
/// payments they collected; cancelling and transferring are for admins.
/// </summary>
[Authorize]
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
    [BindProperty(SupportsGet = true)] public string Status { get; set; } = "all"; // all | paid | pending | cancelled
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public int? CollectorId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CheckpointId { get; set; }
    [BindProperty(SupportsGet = true)] public int? MovementId { get; set; }
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 20;
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public record Row(Payment Payment, string? Account, string? RvNumber);

    public List<Row> Rows { get; set; } = new();
    public Pager Pager { get; set; } = new(0, 1, 20);
    public List<User> Collectors { get; set; } = new();
    public List<Checkpoint> Checkpoints { get; set; } = new();
    public List<Movement> Movements { get; set; } = new();

    public bool IsPersonal { get; private set; }
    public bool IsAdmin { get; private set; }
    public bool CanCancel { get; private set; }

    // Totals over the whole filtered set
    public int PaidCount { get; set; }
    public decimal PaidAmount { get; set; }
    public int PendingCount { get; set; }
    public decimal PendingAmount { get; set; }
    public int CancelledCount { get; set; }
    public decimal CancelledAmount { get; set; }

    public async Task OnGetAsync()
    {
        var me = await MeAsync();
        await LoadAsync(me);
    }

    /// <summary>Cancel (revert) a payment. Admins only, with a reason.</summary>
    public async Task<IActionResult> OnPostCancelAsync(int paymentId, string? reason)
    {
        var me = await MeAsync();
        if (me == null || !IsAdmin || !CanCancel) return Forbid();

        reason = (reason ?? "").Trim();
        var payment = await _context.Payments.Include(p => p.ReceiptReference).FirstOrDefaultAsync(p => p.Id == paymentId);
        var rv = await _context.RvPayments
            .Where(r => r.PaymentId == paymentId && r.RvDocument != null && r.RvDocument.Status != RvStatus.Cancelled)
            .Select(r => r.RvDocument!.RvNumber).FirstOrDefaultAsync();

        string? error =
            payment == null ? "Payment not found." :
            payment.IsReverted ? "This payment is already cancelled." :
            rv != null ? $"Payment #{paymentId} is in RV {rv}. Cancel the RV first." :
            reason.Length < 3 ? "Cancel reason is required." : null;
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToPage(Route());
        }

        payment!.IsReverted = true;
        payment.IsPaid = false;
        payment.RevertedAt = DateTime.UtcNow;
        payment.RevertReason = reason.Length > 500 ? reason[..500] : reason;
        payment.RevertedByUserId = me.Id;

        // Free the receipt reference so it can be used again.
        if (payment.ReceiptReference != null)
        {
            payment.ReceiptReference.IsUsed = false;
            payment.ReceiptReference.UsedAt = null;
            payment.ReceiptReference.UsedBy = null;
            payment.ReceiptReference.VehicleId = null;
        }

        await _context.SaveChangesAsync();
        TempData["Message"] = $"Payment #{payment.Id} ({payment.InvoiceNumber}) was cancelled.";
        return RedirectToPage(Route());
    }

    private object Route() => new
    {
        Search, Status, FromDate = FromDate?.ToString("yyyy-MM-dd"), ToDate = ToDate?.ToString("yyyy-MM-dd"),
        CollectorId, CheckpointId, MovementId, PageSize, CurrentPage,
    };

    private async Task<User?> MeAsync()
    {
        var me = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == User.Identity!.Name);
        var role = me?.Role ?? "";
        IsAdmin = User.IsInRole(AppPermissions.SuperAdminRole) ||
                  role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                  role.Equals(AppPermissions.SuperAdminRole, StringComparison.OrdinalIgnoreCase);
        CanCancel = await _permissions.HasAsync(User, AppPermissions.Keys.PaymentsCancel);
        IsPersonal = role.Equals("Collector", StringComparison.OrdinalIgnoreCase);
        return me;
    }

    private async Task LoadAsync(User? me)
    {
        var q = _context.Payments.AsNoTracking()
            .Include(p => p.Vehicle)
            .Include(p => p.Collector)
            .Include(p => p.Checkpoint)
            .Include(p => p.RevertedByUser)
            .Include(p => p.Movement).ThenInclude(m => m!.RevenueAccount)
            .AsQueryable();

        if (IsPersonal)
        {
            var myId = me?.Id ?? -1;   // unknown user matches nothing
            q = q.Where(p => p.CollectorId == myId);
        }
        else
        {
            Collectors = await _context.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
        }
        Checkpoints = await _context.Checkpoints.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        Movements = await _context.Movements.AsNoTracking().Include(m => m.CarType).OrderBy(m => m.Name).ToListAsync();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            // System ID / Ref No, invoice number (yyyyMM + ID), plate, owner, Golis bill.
            int? n = int.TryParse(s, out var x) ? x : null;
            int? fromInvoice = s.Length > 6 && long.TryParse(s, out _) && int.TryParse(s[6..], out var inv) ? inv : null;
            q = q.Where(p => (n != null && (p.Id == n || p.ReferenceNo == n)) ||
                             (fromInvoice != null && p.Id == fromInvoice) ||
                             (p.Vehicle != null && (p.Vehicle.PlateNumber.Contains(s) || p.Vehicle.OwnerName.Contains(s))) ||
                             (p.TransactionId != null && p.TransactionId.Contains(s)));
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
        if (!IsPersonal && CollectorId.HasValue) q = q.Where(p => p.CollectorId == CollectorId);
        if (CheckpointId.HasValue) q = q.Where(p => p.CheckpointId == CheckpointId);
        if (MovementId.HasValue) q = q.Where(p => p.MovementId == MovementId);

        // Totals by status, before the status filter so the cards stay useful.
        var totals = await q.GroupBy(p => p.IsReverted ? 2 : p.IsPaid ? 0 : 1)
            .Select(g => new { g.Key, N = g.Count(), Sum = g.Sum(p => p.Amount) })
            .ToListAsync();
        (PaidCount, PaidAmount) = totals.Where(t => t.Key == 0).Select(t => (t.N, t.Sum)).FirstOrDefault();
        (PendingCount, PendingAmount) = totals.Where(t => t.Key == 1).Select(t => (t.N, t.Sum)).FirstOrDefault();
        (CancelledCount, CancelledAmount) = totals.Where(t => t.Key == 2).Select(t => (t.N, t.Sum)).FirstOrDefault();

        q = Status switch
        {
            "paid" => q.Where(p => p.IsPaid && !p.IsReverted),
            "pending" => q.Where(p => !p.IsPaid && !p.IsReverted),
            "cancelled" => q.Where(p => p.IsReverted),
            _ => q,
        };
        var total = Status switch
        {
            "paid" => PaidCount, "pending" => PendingCount, "cancelled" => CancelledCount,
            _ => PaidCount + PendingCount + CancelledCount,
        };

        Pager = new Pager(total, CurrentPage, PageSize);
        CurrentPage = Pager.Page;
        var page = await Pager.Apply(q.OrderByDescending(p => p.PaidAt).ThenByDescending(p => p.Id)).ToListAsync();

        var ids = page.Select(p => p.Id).ToList();
        var rvs = (await _context.RvPayments.AsNoTracking()
                .Where(r => ids.Contains(r.PaymentId) && r.RvDocument != null && r.RvDocument.Status != RvStatus.Cancelled)
                .Select(r => new { r.PaymentId, r.RvDocument!.RvNumber })
                .ToListAsync())
            .GroupBy(r => r.PaymentId).ToDictionary(g => g.Key, g => g.First().RvNumber);

        Rows = page.Select(p => new Row(p, p.Movement?.RevenueAccount?.DisplayName,
            rvs.TryGetValue(p.Id, out var rv) ? rv : null)).ToList();
    }
}
