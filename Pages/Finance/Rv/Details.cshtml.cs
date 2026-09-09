using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Finance.Rv;

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

    public RvDocument? Rv { get; set; }
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
        Rv = await _context.RvDocuments
            .Include(r => r.RevenueAccount)
            .Include(r => r.PreparedBy)
            .Include(r => r.TransferredBy)
            .Include(r => r.CreatedBy)
            .Include(r => r.CancelledBy)
            .Include(r => r.Payments).ThenInclude(rp => rp.Payment)
            .Include(r => r.AuditLogs).ThenInclude(l => l.ByUser)
            .FirstOrDefaultAsync(r => r.RvNumber == number);

        if (Rv == null) return false;

        AccountName = Rv.RevenueAccount == null
            ? "-"
            : $"{Rv.RevenueAccount.AccountCode} - {Rv.RevenueAccount.AccountName}";

        // The RF total must exactly equal the sum of its included payments —
        // always verified from the actual payment records.
        PaymentSum = Rv.Payments.Sum(p => p.Amount);

        return true;
    }

    private string FmisLine(string number) =>
        Rv == null ? "" : _fmis.CreateFmisExport(Rv, AccountName, PaymentLines());

    private List<string> PaymentLines() =>
        Rv == null ? new List<string>() : Rv.Payments
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

        var content = _fmis.CreateFmisExport(Rv!, AccountName, PaymentLines());
        return File(System.Text.Encoding.UTF8.GetBytes(content), "text/csv",
            $"FMIS_{Rv!.RvNumber}.csv");
    }

    /// <summary>Advance RF status: Draft/Prepared → ReadyForFmis.</summary>
    public async Task<IActionResult> OnPostMarkReadyAsync(string number)
    {
        if (!CheckFinance()) return RedirectToPage("/Index");
        await LoadAsync(number);
        if (Rv == null) return NotFound();

        if (Rv.Status is RvStatus.Transferred or RvStatus.Cancelled)
        {
            return RedirectToPage(new { number, error = $"RF {Rv.RvNumber} is {Rv.Status} and cannot be modified." });
        }

        var user = await CurrentUserAsync();
        Rv.Status = RvStatus.ReadyForFmis;
        Rv.FmisStatus = FmisTransferStatus.ReadyForFmis;
        Rv.UpdatedAt = DateTime.UtcNow;
        Rv.UpdatedById = user?.Id;
        Rv.AuditLogs.Add(new RvAuditLog
        {
            Action = "Marked Ready for FMIS",
            FromStatus = RvStatus.Prepared,
            ToStatus = RvStatus.ReadyForFmis,
            ActionAt = DateTime.UtcNow,
            ByUserId = user?.Id
        });
        await _context.SaveChangesAsync();

        return RedirectToPage(new { number, message = $"RF {Rv.RvNumber} is ready for FMIS transfer." });
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
        if (Rv == null) return NotFound();

        // Never transfer twice
        if (Rv.Status == RvStatus.Transferred)
        {
            return RedirectToPage(new { number, error = "This RF has already been transferred to FMIS. It cannot be transferred twice." });
        }
        if (Rv.Status == RvStatus.Cancelled)
        {
            return RedirectToPage(new { number, error = "This RF is cancelled and cannot be transferred." });
        }

        // Validate before transfer
        if (PaymentSum != Rv.TotalAmount || Rv.Payments.Count != Rv.TotalTransactions)
        {
            return RedirectToPage(new { number, error = "Amount totals do not match the included payments — transfer blocked." });
        }

        var user = await CurrentUserAsync();
        Rv.Status = RvStatus.ReadyForFmis;
        Rv.FmisStatus = FmisTransferStatus.ReadyForFmis;

        var result = await _fmis.PostToFmisAsync(Rv, AccountName, PaymentLines());

        Rv.AuditLogs.Add(new RvAuditLog
        {
            Action = "FMIS Transfer Attempted",
            Details = result.Success ? "FMIS accepted the batch." : result.Message,
            ActionAt = DateTime.UtcNow,
            ByUserId = user?.Id
        });

        if (result.Success)
        {
            Rv.Status = RvStatus.Transferred;
            Rv.FmisStatus = FmisTransferStatus.Transferred;
            Rv.FmisBatchNumber = result.BatchNumber;
            Rv.FmisResponse = result.Message;
            Rv.TransferredAt = DateTime.UtcNow;
            Rv.TransferredById = user?.Id;
            Rv.AuditLogs.Add(new RvAuditLog
            {
                Action = "FMIS Transferred",
                ToStatus = RvStatus.Transferred,
                Details = $"FMIS Batch No.: {result.BatchNumber}",
                ActionAt = DateTime.UtcNow,
                ByUserId = user?.Id
            });
            await _context.SaveChangesAsync();
            return RedirectToPage(new { number, message = $"Transferred to FMIS successfully. Batch No.: {result.BatchNumber}" });
        }

        if (!result.ManualMode)
        {
            Rv.FmisStatus = FmisTransferStatus.Failed;
            Rv.FmisResponse = result.Message;
            Rv.AuditLogs.Add(new RvAuditLog
            {
                Action = "FMIS Failed",
                Details = result.Message,
                ActionAt = DateTime.UtcNow,
                ByUserId = user?.Id
            });
            await _context.SaveChangesAsync();
            return RedirectToPage(new { number, error = $"FMIS transfer failed: {result.Message} You can retry — no second RF is created." });
        }

        Rv.AuditLogs.Add(new RvAuditLog
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
        if (Rv == null) return NotFound();

        if (Rv.Status == RvStatus.Transferred)
        {
            return RedirectToPage(new { number, error = "This RF has already been transferred to FMIS." });
        }
        if (Rv.Status == RvStatus.Cancelled)
        {
            return RedirectToPage(new { number, error = "This RF is cancelled and cannot be transferred." });
        }

        var user = await CurrentUserAsync();
        var result = _fmis.ConfirmManualTransfer(Rv, user?.Id);

        Rv.Status = RvStatus.Transferred;
        Rv.FmisStatus = FmisTransferStatus.Transferred;
        Rv.FmisBatchNumber = result.BatchNumber;
        Rv.TransferredAt = DateTime.UtcNow;
        Rv.TransferredById = user?.Id;
        Rv.AuditLogs.Add(new RvAuditLog
        {
            Action = "FMIS Transferred (Manual Confirmation)",
            ToStatus = RvStatus.Transferred,
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
        if (Rv == null) return NotFound();

        if (Rv.Status == RvStatus.Transferred)
        {
            return RedirectToPage(new { number, error = "A transferred RF cannot be cancelled." });
        }
        if (Rv.Status == RvStatus.Cancelled)
        {
            return RedirectToPage(new { number, error = "This RF is already cancelled." });
        }

        var user = await CurrentUserAsync();
        var fromStatus = Rv.Status;
        Rv.Status = RvStatus.Cancelled;
        Rv.CancelledAt = DateTime.UtcNow;
        Rv.CancelledById = user?.Id;
        Rv.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by Finance" : reason;
        Rv.UpdatedAt = DateTime.UtcNow;
        Rv.UpdatedById = user?.Id;

        // Release the payments so they can be included in a future RF
        _context.RvPayments.RemoveRange(Rv.Payments);

        Rv.AuditLogs.Add(new RvAuditLog
        {
            Action = "Cancelled",
            FromStatus = fromStatus,
            ToStatus = RvStatus.Cancelled,
            Details = $"{Rv.CancellationReason}. Payments released for future RF inclusion. RF record preserved.",
            ActionAt = DateTime.UtcNow,
            ByUserId = user?.Id
        });
        await _context.SaveChangesAsync();

        return RedirectToPage(new { number, message = $"RF {Rv.RvNumber} cancelled. The RF record is preserved; its payments are available for a new RF." });
    }

    private async Task<User?> CurrentUserAsync()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return null;
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }
}

