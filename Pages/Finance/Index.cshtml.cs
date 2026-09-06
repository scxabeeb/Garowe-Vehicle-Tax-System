using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Finance;

/// <summary>
/// Finance / Accountant dashboard — Today's (or selected date) successfully PAID
/// payments, grouped by financial (revenue) account, with RF / FMIS reconciliation.
/// Pending / Failed / unrecorded / cancelled-before-recording payments are NEVER shown.
/// </summary>
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    public IndexModel(AppDbContext context) { _context = context; }

    public bool IsFinance { get; private set; }

    [BindProperty(SupportsGet = true)] public DateTime? Date { get; set; }

    // ── Today's payments ──────────────────────────────────────────
    public List<FinancePaymentRow> Payments { get; set; } = new();
    public int TotalPayments { get; set; }
    public decimal TotalAmount { get; set; }

    // ── Account summary ───────────────────────────────────────────
    public List<AccountGroup> AccountSummary { get; set; } = new();

    // ── RF / FMIS reconciliation ──────────────────────────────────
    public int RfPrepared { get; set; }
    public int RfReadyForFmis { get; set; }
    public int RfTransferred { get; set; }
    public int RfFailed { get; set; }
    public int NotYetInRf { get; set; }
    public decimal NotYetInRfAmount { get; set; }
    public int IncludedInRf { get; set; }
    public decimal IncludedInRfAmount { get; set; }
    public int TransferredTo_Fmis { get; set; }
    public decimal TransferredTo_FmisAmount { get; set; }

    public class FinancePaymentRow
    {
        public int PaymentId { get; set; }
        public int? ReferenceNo { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public string? ReceiptNo { get; set; }
        public string? GolisBillNo { get; set; }
        public string Vehicle { get; set; } = "";
        public string? Account { get; set; }
        public int? RevenueAccountId { get; set; }
        public decimal Amount { get; set; }
        public string? Collector { get; set; }
        public DateTime PaidAt { get; set; }
        public string Status { get; set; } = "Paid";
        public string FmisStatus { get; set; } = "Not Included";
        public string? RfNumber { get; set; }
    }

    public class AccountGroup
    {
        public int? RevenueAccountId { get; set; }
        public string AccountName { get; set; } = "";
        public int Transactions { get; set; }
        public decimal Total { get; set; }
    }

    private bool ComputeIsFinance() =>
        User.IsInRole("Admin") || User.HasClaim("permission", "finance.manage");

    private IActionResult? CheckFinance()
    {
        IsFinance = ComputeIsFinance();
        if (!IsFinance)
        {
            return RedirectToPage("/Index");
        }
        return null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var denied = CheckFinance();
        if (denied != null) return denied;

        Date ??= AppTime.Today;

        var dayStart = Date.Value.Date;
        var dayEnd = dayStart.AddDays(1);

        // ONLY successfully recorded (Paid) payments — never pending/failed/unrecorded.
        var payments = await _context.Payments
            .Include(p => p.Vehicle)
            .Include(p => p.Movement).ThenInclude(m => m!.RevenueAccount)
            .Include(p => p.Collector)
            .Include(p => p.ReceiptReference)
            .Where(p => p.IsPaid && !p.IsReverted)
            .Where(p => p.PaidAt >= dayStart && p.PaidAt < dayEnd)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        // Which payments are already included in an RF / transferred to FMIS?
        var paymentIds = payments.Select(p => p.Id).ToList();
        var rfLinks = await _context.RfPayments
            .Include(rp => rp.RfDocument)
            .Where(rp => paymentIds.Contains(rp.PaymentId))
            .ToDictionaryAsync(rp => rp.PaymentId, rp => rp.RfDocument!);

        foreach (var p in payments)
        {
            var row = new FinancePaymentRow
            {
                PaymentId = p.Id,
                ReferenceNo = p.ReferenceNo,
                InvoiceNumber = p.InvoiceNumber,
                ReceiptNo = p.ReceiptReference != null ? $"RCT{p.ReceiptReference.Id}" : null,
                GolisBillNo = p.TransactionId,
                Vehicle = p.Vehicle?.PlateNumber ?? $"Vehicle #{p.VehicleId}",
                RevenueAccountId = p.Movement?.RevenueAccountId,
                Account = p.Movement?.RevenueAccount == null
                    ? null
                    : $"{p.Movement.RevenueAccount.AccountCode} - {p.Movement.RevenueAccount.AccountName}",
                Amount = p.Amount,
                Collector = p.Collector?.FullName ?? p.Collector?.Username,
                PaidAt = p.PaidAt,
                Status = "Paid"
            };

            if (rfLinks.TryGetValue(p.Id, out var rf) && rf.Status != RfStatus.Cancelled)
            {
                row.RfNumber = rf.RfNumber;
                row.FmisStatus = rf.Status == RfStatus.Transferred
                    ? "Transferred"
                    : rf.FmisStatus == FmisTransferStatus.Failed
                        ? "FMIS Failed"
                        : "Included in RF";
            }

            Payments.Add(row);
        }

        TotalPayments = Payments.Count;
        TotalAmount = Payments.Sum(x => x.Amount);

        // Group by financial account
        AccountSummary = Payments
            .GroupBy(x => x.RevenueAccountId ?? 0)
            .Select(g => new AccountGroup
            {
                RevenueAccountId = g.Key == 0 ? null : g.Key,
                AccountName = g.First().Account ?? "Unmapped Account",
                Transactions = g.Count(),
                Total = g.Sum(x => x.Amount)
            })
            .OrderByDescending(a => a.Total)
            .ToList();

        // RF status counters for the selected date
        var rfs = await _context.RfDocuments
            .Where(r => r.RfDate >= dayStart && r.RfDate < dayEnd)
            .ToListAsync();
        RfPrepared = rfs.Count(r => r.Status == RfStatus.Prepared);
        RfReadyForFmis = rfs.Count(r => r.Status == RfStatus.ReadyForFmis);
        RfTransferred = rfs.Count(r => r.Status == RfStatus.Transferred);
        RfFailed = rfs.Count(r => r.FmisStatus == FmisTransferStatus.Failed);

        // Reconciliation — Paid → Included in RF → Transferred to FMIS
        IncludedInRf = Payments.Count(x => x.RfNumber != null);
        IncludedInRfAmount = Payments.Where(x => x.RfNumber != null).Sum(x => x.Amount);
        TransferredTo_Fmis = Payments.Count(x => x.FmisStatus == "Transferred");
        TransferredTo_FmisAmount = Payments.Where(x => x.FmisStatus == "Transferred").Sum(x => x.Amount);
        NotYetInRf = TotalPayments - IncludedInRf;
        NotYetInRfAmount = TotalAmount - IncludedInRfAmount;

        return Page();
    }
}

