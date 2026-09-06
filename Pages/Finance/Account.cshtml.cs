using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Finance;

/// <summary>
/// Account detail — all successfully PAID payments for the selected date belonging to one
/// financial (revenue) account. The Accountant selects eligible payments and generates an
/// RF (Finance batch document) — Collectors can NEVER do this.
/// </summary>
public class AccountModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IRfNumberService _rfNumberService;

    public AccountModel(AppDbContext context, IRfNumberService rfNumberService)
    {
        _context = context;
        _rfNumberService = rfNumberService;
    }

    public bool IsFinance { get; private set; }

    [BindProperty(SupportsGet = true)] public DateTime? Date { get; set; }
    [BindProperty(SupportsGet = true)] public int? AccountId { get; set; }

    public string AccountName { get; set; } = "";
    public int Transactions { get; set; }
    public decimal Total { get; set; }

    public List<PaymentRow> Rows { get; set; } = new();

    [BindProperty] public List<int> SelectedPayments { get; set; } = new();
    public string? Message { get; private set; }
    public string? ErrorMessage { get; private set; }

    public class PaymentRow
    {
        public int PaymentId { get; set; }
        public int? ReferenceNo { get; set; }
        public string? ReceiptNo { get; set; }
        public decimal Amount { get; set; }
        public string? Collector { get; set; }
        public string Status { get; set; } = "Paid";
        public bool Eligible { get; set; }
        public string? RfNumber { get; set; }
    }

    private IActionResult? CheckFinance()
    {
        IsFinance = User.IsInRole("Admin") || User.HasClaim("permission", "finance.manage");
        return IsFinance ? null : RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnGetAsync(string? message = null, string? error = null)
    {
        var denied = CheckFinance();
        if (denied != null) return denied;

        Message = message;
        ErrorMessage = error;
        Date ??= AppTime.Today;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var dayStart = Date!.Value.Date;
        var dayEnd = dayStart.AddDays(1);

        var query = _context.Payments
            .Include(p => p.Collector)
            .Include(p => p.Movement).ThenInclude(m => m!.RevenueAccount)
            .Include(p => p.ReceiptReference)
            .Where(p => p.IsPaid && !p.IsReverted)
            .Where(p => p.PaidAt >= dayStart && p.PaidAt < dayEnd);

        if (AccountId.HasValue && AccountId.Value > 0)
        {
            query = query.Where(p => p.Movement!.RevenueAccountId == AccountId.Value);
        }

        var payments = await query.OrderByDescending(p => p.Id).ToListAsync();

        // Account header
        if (AccountId.HasValue && AccountId.Value > 0)
        {
            var acc = await _context.RevenueAccounts.FirstOrDefaultAsync(a => a.Id == AccountId.Value);
            AccountName = acc == null ? "Unknown Account" : $"{acc.AccountCode} - {acc.AccountName}";
        }
        else
        {
            AccountName = "All Accounts";
        }

        // RF membership for these payments
        var ids = payments.Select(p => p.Id).ToList();
        var rfLinks = await _context.RfPayments
            .Include(rp => rp.RfDocument)
            .Where(rp => ids.Contains(rp.PaymentId))
            .ToDictionaryAsync(rp => rp.PaymentId, rp => rp.RfDocument!);

        foreach (var p in payments)
        {
            rfLinks.TryGetValue(p.Id, out var rf);
            var inActiveRf = rf != null && rf.Status != RfStatus.Cancelled;

            Rows.Add(new PaymentRow
            {
                PaymentId = p.Id,
                ReferenceNo = p.ReferenceNo,
                ReceiptNo = p.ReceiptReference != null ? $"RCT{p.ReceiptReference.Id}" : null,
                Amount = p.Amount,
                Collector = p.Collector?.FullName ?? p.Collector?.Username,
                Status = "Paid",
                Eligible = !inActiveRf,
                RfNumber = inActiveRf ? rf!.RfNumber : null
            });
        }

        Transactions = Rows.Count;
        Total = Rows.Sum(r => r.Amount);
    }

    public async Task<IActionResult> OnPostGenerateRfAsync()
    {
        var denied = CheckFinance();
        if (denied != null) return denied;

        Date ??= AppTime.Today;

        if (SelectedPayments == null || SelectedPayments.Count == 0)
        {
            ErrorMessage = "No payments selected. Select at least one payment.";
            await LoadAsync();
            return Page();
        }

        var dayStart = Date.Value.Date;
        var dayEnd = dayStart.AddDays(1);

        // Re-validate every selected payment SERVER-SIDE (never trust the client):
        // must be Paid, not reverted, within the date period, matching the account,
        // and not already included in an active (non-cancelled) RF.
        var payments = await _context.Payments
            .Include(p => p.Movement).ThenInclude(m => m!.RevenueAccount)
            .Where(p => SelectedPayments.Contains(p.Id))
            .ToListAsync();

        var existingPaymentIds = await _context.RfPayments
            .Include(rp => rp.RfDocument)
            .Where(rp => SelectedPayments.Contains(rp.PaymentId) && rp.RfDocument!.Status != RfStatus.Cancelled)
            .Select(rp => new { rp.PaymentId, rp.RfDocument!.RfNumber })
            .ToListAsync();

        if (existingPaymentIds.Any())
        {
            var dup = existingPaymentIds.First();
            ErrorMessage = $"Payment {dup.PaymentId} has already been included in {dup.RfNumber}. A payment cannot be included in more than one RF.";
            await LoadAsync();
            return Page();
        }

        var invalid = payments
            .Where(p => !p.IsPaid || p.IsReverted || p.PaidAt < dayStart || p.PaidAt >= dayEnd)
            .ToList();
        if (invalid.Any())
        {
            ErrorMessage = $"Only successfully PAID payments within the selected date can be included ({invalid.Count} invalid).";
            await LoadAsync();
            return Page();
        }

        // All selected payments must belong to the same financial account
        var accountIds = payments
            .Select(p => p.Movement?.RevenueAccountId)
            .Distinct()
            .ToList();
        if (accountIds.Count > 1)
        {
            ErrorMessage = "All payments in one RF must belong to the same financial account.";
            await LoadAsync();
            return Page();
        }
        if (AccountId.HasValue && AccountId.Value > 0 && accountIds.FirstOrDefault() != AccountId.Value)
        {
            ErrorMessage = "Account information is missing or mismatched.";
            await LoadAsync();
            return Page();
        }

        var revenueAccountId = accountIds.FirstOrDefault();

        // Totals are ALWAYS computed from the actual backend payment records
        var totalAmount = payments.Sum(p => p.Amount);
        if (totalAmount <= 0)
        {
            ErrorMessage = "Amount totals do not match — total must be greater than zero.";
            await LoadAsync();
            return Page();
        }

        // Unique sequential RF number generated by the backend ONLY
        var rfNumber = _rfNumberService.GetNextRfNumber();

        var currentUsername = User.Identity?.Name;
        var currentUser = string.IsNullOrWhiteSpace(currentUsername)
            ? null
            : await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);

        var rf = new RfDocument
        {
            RfNumber = rfNumber,
            RfDate = AppTime.Today,
            RevenueAccountId = revenueAccountId,
            PeriodFrom = dayStart,
            PeriodTo = dayEnd,
            TotalTransactions = payments.Count,
            TotalAmount = totalAmount,
            Status = RfStatus.Prepared,
            FmisStatus = FmisTransferStatus.NotTransferred,
            PreparedById = currentUser?.Id,
            CreatedById = currentUser?.Id,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var p in payments)
        {
            rf.Payments.Add(new RfPayment
            {
                PaymentId = p.Id,
                ReferenceNo = p.ReferenceNo,
                Amount = p.Amount,
                PaidAt = p.PaidAt,
                InvoiceNumber = p.InvoiceNumber,
                CollectBy = p.Collector?.FullName ?? p.Collector?.Username
            });
        }

        rf.AuditLogs.Add(new RfAuditLog
        {
            Action = "RF Created",
            ToStatus = RfStatus.Prepared,
            Details = $"Created with {payments.Count} payment(s), total ${totalAmount:0.00} from account {revenueAccountId}.",
            ActionAt = DateTime.UtcNow,
            ByUserId = currentUser?.Id
        });

        _context.RfDocuments.Add(rf);
        await _context.SaveChangesAsync();

        return RedirectToPage("/Finance/Rf/Details", new { number = rf.RfNumber });
    }
}
