using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Finance.Rf;

/// <summary>
/// RF document details — the Finance batch, its included payments, and the FMIS transfer
/// workflow: Prepared → Ready for FMIS → Transferred. Includes FMIS export download, live
/// transfer, manual-post confirmation, retry on failure (same RF, never a second RF) and
/// cancellation (RF record preserved, never silently deleted).
/// </summary>
public class DetailsModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IFmisTransferService _fmis;

    public DetailsModel(AppDbContext context, IFmisTransferService fmis)
    {
        _context = context;
        _fmis = fmis;
    }

    public bool IsFinance { get; private set; }

    public RfDocument? Rf { get; set; }
    public string AccountName { get; set; } = "-";
    public decimal PaymentSum { get; set; }
    public string? Message { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(string number, string? message = null, string? error = null)
    {
        var denied = CheckFinance();
        if (!denied) return RedirectToPage("/Index");

        Message = message;
        ErrorMessage = error;
        var loaded = await LoadAsync(number);
        if (!loaded) return NotFound();
        return Page();
    }

    private bool CheckFinance()
    {
        IsFinance = User.IsInRole("Admin") || User.HasClaim("permission", "finance.manage");
        return IsFinance;
    }

    private async Task<bool> LoadAsync(string number)
    {
        Rf = await _context.RfDocuments
            .Include(r => r.RevenueAccount)
            .Include(r => r.PreparedBy)
            .Include(r => r.TransferredBy)
            .Include(r => r.CreatedBy)
            .Include(r => r.CancelledBy)
            .Include(r => r.Payments).ThenInclude(rp => rp.Payment)
            .Include(r => r.AuditLogs).ThenInclude(l => l.ByUser)
            .FirstOrDefaultAsync(r => r.RfNumber == number);

        if (Rf == null) return false;

        AccountName = Rf.RevenueAccount == null
            ? "-"
            : $"{Rf.RevenueAccount.AccountCode} - {Rf.RevenueAccount.AccountName}";

        // The RF total must exactly equal the sum of its included payments —
        // always verified from the actual payment records.
        PaymentSum = Rf.Payments.Sum(p => p.Amount);

        return true;
    }

    private string FmisLine(string number) =>
        Rf == null ? "" : _fmis.CreateFmisExport(Rf, AccountName, PaymentLines());

    private List<string> PaymentLines() =>
        Rf == null ? new List<string>() : Rf.Payments
            .OrderBy(p => p.ReferenceNo ?? p.Id)
            .Select(p => string.Join(',',
                p.ReferenceNo?.ToString() ?? "-",
                p.PaymentId,
                p.InvoiceNumber ?? "",
                p.Amount.ToString("0.00"),
                AppTime.ToLocal(p.PaidAt).ToString("yyyy-MM-dd HH:mm:ss"),
                (p.CollectBy ?? "").Replace(",", " ")))
            .ToList();

/// <summary>
    /// <summary>Download the FMIS batch upload file (manual transfer mode).</summary>
    public async Task<IActionResult> OnGetExportAsync(string number)
    {
        if (!CheckFinance()) return RedirectToPage("/Index");
        var loaded = await LoadAsync(number);
        if (!loaded) return NotFound();

        var content = _fmis.CreateFmisExport(Rf!, AccountName, PaymentLines());
        return File(System.Text.Encoding.UTF8.GetBytes(content), "text/csv",
            $"FMIS_{Rf!.RfNumber}.csv");
    }

    /// <summary>Advance RF status: Draft/Prepared → ReadyForFmis.</summary>
    public async Task<IActionResult> OnPostMarkReadyAsync(string number)
    {
        if (!CheckFinance()) return RedirectToPage("/Index");
        await LoadAsync(number);
        if (Rf == null) return NotFound();

        if (Rf.Status is RfStatus.Transferred or RfStatus.Cancelled)
        {
            return RedirectToPage(new { number, error = $"RF {Rf.RfNumber} is {Rf.Status} and cannot be modified." });
        }

        var user = await CurrentUserAsync();
        Rf.Status = RfStatus.ReadyForFmis;
        Rf.FmisStatus = FmisTransferStatus.ReadyForFmis;
        Rf.UpdatedAt = DateTime.UtcNow;
        Rf.UpdatedById = user?.Id;
        Rf.AuditLogs.Add(new RfAuditLog
        {
            Action = "Marked Ready for FMIS",
            FromStatus = RfStatus.Prepared,
            ToStatus = RfStatus.ReadyForFmis,
            ActionAt = DateTime.UtcNow,
            ByUserId = user?.Id
        });
        await _context.SaveChangesAsync();

        return RedirectToPage(new { number, message = $"RF {Rf.RfNumber} is ready for FMIS transfer." });
    }

    /// <summary>
    /// Transfer the RF to FMIS. On success → RF + payments = Transferred, FMIS batch number
    /// stored. On failure → FmisStatus = Failed with the real error; payments are NOT marked
    /// transferred and the same RF can be retried (no second RF is created).
    /// </summary>
    public async Task<IActionResult> OnPostTransferAsync(string number)
    {
        if (!CheckFinance()) return RedirectToPage("/Index");
        await LoadAsync(number);
        if (Rf == null) return NotFound();

        // Never transfer twice
        if (Rf.Status == RfStatus.Transferred)
        {
            return RedirectToPage(new { number, error = "This RF has already been transferred to FMIS. It cannot be transferred twice." });
        }
        if (Rf.Status == RfStatus.Cancelled)
        {
            return RedirectToPage(new { number, error = "This RF is cancelled and cannot be transferred." });
        }

        // Validate before transfer
        if (PaymentSum != Rf.TotalAmount || Rf.Payments.Count != Rf.TotalTransactions)
        {
            return RedirectToPage(new { number, error = "Amount totals do not match the included payments — transfer blocked." });
        }

        var user = await CurrentUserAsync();
        Rf.Status = RfStatus.ReadyForFmis;
        Rf.FmisStatus = FmisTransferStatus.ReadyForFmis;

        var result = await _fmis.PostToFmisAsync(Rf, AccountName, PaymentLines());

        Rf.AuditLogs.Add(new RfAuditLog
        {
            Action = "FMIS Transfer Attempted",
            Details = result.Success ? "FMIS accepted the batch." : result.Message,
            ActionAt = DateTime.UtcNow,
            ByUserId = user?.Id
        });

        if (result.Success)
        {
            Rf.Status = RfStatus.Transferred;
            Rf.FmisStatus = FmisTransferStatus.Transferred;
            Rf.FmisBatchNumber = result.BatchNumber;
            Rf.FmisResponse = result.Message;
            Rf.TransferredAt = DateTime.UtcNow;
            Rf.TransferredById = user?.Id;
            Rf.AuditLogs.Add(new RfAuditLog
            {
                Action = "FMIS Transferred",
                ToStatus = RfStatus.Transferred,
                Details = $"FMIS Batch No.: {result.BatchNumber}",
                ActionAt = DateTime.UtcNow,
                ByUserId = user?.Id
            });
            await _context.SaveChangesAsync();
            return RedirectToPage(new { number, message = $"Transferred to FMIS successfully. Batch No.: {result.BatchNumber}" });
        }

        if (!result.ManualMode)
        {
            Rf.FmisStatus = FmisTransferStatus.Failed;
            Rf.FmisResponse = result.Message;
            Rf.AuditLogs.Add(new RfAuditLog
            {
                Action = "FMIS Failed",
                Details = result.Message,
                ActionAt = DateTime.UtcNow,
                ByUserId = user?.Id
            });
            await _context.SaveChangesAsync();
            return RedirectToPage(new { number, error = $"FMIS transfer failed: {result.Message} You can retry — no second RF is created." });
        }

        Rf.AuditLogs.Add(new RfAuditLog
        {
            Action = "FMIS Manual Mode",
            Details = result.Message,
            ActionAt = DateTime.UtcNow,
            ByUserId = user?.Id
        });
        await _context.SaveChangesAsync();
        return RedirectToPage(new
        {
            number,
            error = "No live FMIS integration is configured. Download the FMIS export, post it manually, then confirm the manual transfer."
        });
    }

    /// <summary>Accountant confirms they manually posted the export file to FMIS.</summary>
    public async Task<IActionResult> OnPostConfirmManualAsync(string number)
    {
        if (!CheckFinance()) return RedirectToPage("/Index");
        await LoadAsync(number);
        if (Rf == null) return NotFound();

        if (Rf.Status == RfStatus.Transferred)
        {
            return RedirectToPage(new { number, error = "This RF has already been transferred to FMIS." });
        }
        if (Rf.Status == RfStatus.Cancelled)
        {
            return RedirectToPage(new { number, error = "This RF is cancelled and cannot be transferred." });
        }

        var user = await CurrentUserAsync();
        var result = _fmis.ConfirmManualTransfer(Rf, user?.Id);

        Rf.Status = RfStatus.Transferred;
        Rf.FmisStatus = FmisTransferStatus.Transferred;
        Rf.FmisBatchNumber = result.BatchNumber;
        Rf.TransferredAt = DateTime.UtcNow;
        Rf.TransferredById = user?.Id;
        Rf.AuditLogs.Add(new RfAuditLog
        {
            Action = "FMIS Transferred (Manual Confirmation)",
            ToStatus = RfStatus.Transferred,
            Details = $"Manually posted to FMIS by {user?.Username}. FMIS Batch No.: {result.BatchNumber}",
            ActionAt = DateTime.UtcNow,
            ByUserId = user?.Id
        });
        await _context.SaveChangesAsync();

        return RedirectToPage(new { number, message = $"Manual FMIS post confirmed. Batch No.: {result.BatchNumber}" });
    }

    /// <summary>
    /// Cancel the RF (only if not yet transferred). The RF record and its audit trail are
    /// PRESERVED — never silently deleted. Its payments are released so they can be
    /// included in a future RF.
    /// </summary>
    public async Task<IActionResult> OnPostCancelAsync(string number, string? reason)
    {
        if (!CheckFinance()) return RedirectToPage("/Index");
        await LoadAsync(number);
        if (Rf == null) return NotFound();

        if (Rf.Status == RfStatus.Transferred)
        {
            return RedirectToPage(new { number, error = "A transferred RF cannot be cancelled." });
        }
        if (Rf.Status == RfStatus.Cancelled)
        {
            return RedirectToPage(new { number, error = "This RF is already cancelled." });
        }

        var user = await CurrentUserAsync();
        var fromStatus = Rf.Status;
        Rf.Status = RfStatus.Cancelled;
        Rf.CancelledAt = DateTime.UtcNow;
        Rf.CancelledById = user?.Id;
        Rf.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by Finance" : reason;
        Rf.UpdatedAt = DateTime.UtcNow;
        Rf.UpdatedById = user?.Id;

        // Release the payments so they can be included in a future RF
        _context.RfPayments.RemoveRange(Rf.Payments);

        Rf.AuditLogs.Add(new RfAuditLog
        {
            Action = "Cancelled",
            FromStatus = fromStatus,
            ToStatus = RfStatus.Cancelled,
            Details = $"{Rf.CancellationReason}. Payments released for future RF inclusion. RF record preserved.",
            ActionAt = DateTime.UtcNow,
            ByUserId = user?.Id
        });
        await _context.SaveChangesAsync();

        return RedirectToPage(new { number, message = $"RF {Rf.RfNumber} cancelled. The RF record is preserved; its payments are available for a new RF." });
    }

    private async Task<User?> CurrentUserAsync()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return null;
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }
}

