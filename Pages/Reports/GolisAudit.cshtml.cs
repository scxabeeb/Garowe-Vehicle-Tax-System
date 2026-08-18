using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Reports;

[Authorize(Roles = "Admin,Finance Officer,Auditor,Manager")]
public class GolisAuditModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public GolisAuditModel(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [BindProperty(SupportsGet = true)]
    public int? AuditId { get; set; }

    [BindProperty]
    public AuditInputModel Input { get; set; } = new();

    public GolisAudit? Audit { get; set; }
    public List<GolisTransactionViewModel> GolisRows { get; set; } = new();
    public List<SystemOnlyViewModel> SystemOnlyRows { get; set; } = new();

    // Dashboard totals
    public int TotalGolisTransactions { get; set; }
    public decimal TotalGolisAmount { get; set; }
    public int TotalSystemTransactions { get; set; }
    public decimal TotalSystemAmount { get; set; }
    public int MatchedCount { get; set; }
    public decimal MatchedAmount { get; set; }
    public int NotInSystemCount { get; set; }
    public decimal NotInSystemAmount { get; set; }
    public int AmountMismatchCount { get; set; }
    public decimal AmountMismatchAmount { get; set; }
    public int DuplicateCount { get; set; }
    public int SystemOnlyCount { get; set; }
    public decimal SystemOnlyAmount { get; set; }
    public decimal Difference { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public bool IsReadOnly => Audit?.Status == AuditStatus.Finalized;

    public void OnGet()
    {
        if (AuditId.HasValue)
        {
            LoadAudit(AuditId.Value);
        }
    }

    public IActionResult OnPostCreate()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = GetCurrentUser();
        var audit = new GolisAudit
        {
            StatementNumber = Input.StatementNumber,
            AuditPeriodFrom = Input.FromDate.Date,
            AuditPeriodTo = Input.ToDate.Date,
            StatementTotal = Input.StatementTotal,
            StatementTransactionCount = Input.StatementTransactionCount,
            Notes = Input.Notes,
            CreatedByUserId = user?.Id,
            Status = AuditStatus.Draft
        };

        _context.GolisAudits.Add(audit);
        _context.SaveChanges();

        return RedirectToPage(new { auditId = audit.Id });
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? uploadedFile)
    {
        if (!AuditId.HasValue)
            return NotFound();

        var audit = _context.GolisAudits
            .Include(a => a.GolisTransactions)
            .FirstOrDefault(a => a.Id == AuditId.Value);

        if (audit == null || audit.Status == AuditStatus.Finalized)
            return NotFound();

        if (uploadedFile != null && uploadedFile.Length > 0)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "golis");
            Directory.CreateDirectory(uploadsFolder);
            var fileName = $"{audit.Id}_{Guid.NewGuid()}{Path.GetExtension(uploadedFile.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            using (var stream = System.IO.File.Create(filePath))
            {
                await uploadedFile.CopyToAsync(stream);
            }
            audit.UploadedFilePath = $"/uploads/golis/{fileName}";
            _context.SaveChanges();
        }

        return RedirectToPage(new { auditId = AuditId });
    }

    public IActionResult OnPostAddTransaction(GolisTransactionInputModel transaction)
    {
        if (!AuditId.HasValue)
            return NotFound();

        var audit = _context.GolisAudits
            .Include(a => a.GolisTransactions)
            .FirstOrDefault(a => a.Id == AuditId.Value);

        if (audit == null || audit.Status == AuditStatus.Finalized)
            return NotFound();

        var user = GetCurrentUser();
        var golisTx = new GolisTransaction
        {
            GolisAuditId = audit.Id,
            GolisTransactionReference = transaction.Reference,
            TransactionDate = transaction.Date.Date,
            TransactionTime = transaction.Time,
            MobileNumber = transaction.MobileNumber,
            Amount = transaction.Amount,
            Description = transaction.Description,
            GolisStatementNumber = audit.StatementNumber,
            AuditPeriod = $"{audit.AuditPeriodFrom:yyyy-MM-dd} to {audit.AuditPeriodTo:yyyy-MM-dd}",
            EnteredByUserId = user?.Id,
            Notes = transaction.Notes,
            ReconciliationStatus = GolisReconciliationStatus.NeedsReview
        };

        _context.GolisTransactions.Add(golisTx);
        _context.SaveChanges();

        ReconcileAudit(audit.Id);

        return RedirectToPage(new { auditId = audit.Id });
    }

    public IActionResult OnPostReconcile(int auditId)
    {
        var audit = _context.GolisAudits
            .Include(a => a.GolisTransactions)
            .FirstOrDefault(a => a.Id == auditId);

        if (audit == null || audit.Status == AuditStatus.Finalized)
            return NotFound();

        ReconcileAudit(audit.Id);

        return RedirectToPage(new { auditId = audit.Id });
    }

    public IActionResult OnPostFinalize(int auditId)
    {
        var audit = _context.GolisAudits
            .Include(a => a.GolisTransactions)
            .FirstOrDefault(a => a.Id == auditId);

        if (audit == null || audit.Status == AuditStatus.Finalized)
            return NotFound();

        ReconcileAudit(audit.Id);

        var user = GetCurrentUser();
        audit.Status = AuditStatus.Finalized;
        audit.IsFinalized = true;
        audit.FinalizedAt = DateTime.UtcNow;
        audit.FinalizedByUserId = user?.Id;
        _context.SaveChanges();

        return RedirectToPage(new { auditId = audit.Id });
    }

    public IActionResult OnPostReopen(int auditId)
    {
        if (!User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "Only administrators can reopen a finalized audit.";
            return RedirectToPage(new { auditId });
        }

        var audit = _context.GolisAudits.Find(auditId);
        if (audit == null)
            return NotFound();

        audit.Status = AuditStatus.Reopened;
        audit.IsFinalized = false;
        audit.FinalizedAt = null;
        audit.FinalizedByUserId = null;
        _context.SaveChanges();

        return RedirectToPage(new { auditId = audit.Id });
    }

    public IActionResult OnPostDeleteTransaction(int transactionId)
    {
        var tx = _context.GolisTransactions.Find(transactionId);
        if (tx == null)
            return NotFound();

        var auditId = tx.GolisAuditId;
        _context.GolisTransactions.Remove(tx);
        _context.SaveChanges();

        ReconcileAudit(auditId);

        return RedirectToPage(new { auditId });
    }

    public IActionResult OnGetExport(int auditId, string type)
    {
        var audit = _context.GolisAudits
            .Include(a => a.GolisTransactions)
            .FirstOrDefault(a => a.Id == auditId);

        if (audit == null)
            return NotFound();

        ReconcileAudit(audit.Id);
        LoadAudit(audit.Id);

        var sb = new StringBuilder();
        if (type == "golis")
        {
            sb.AppendLine("Golis Reference,Date,Time,Mobile,Amount,Description,System Receipt,System Amount,Difference,Status,Notes");
            foreach (var row in GolisRows)
            {
                sb.AppendLine($"{row.Reference},{row.Date:yyyy-MM-dd},{row.Time},{row.Mobile},{row.Amount},{Escape(row.Description)},{row.SystemReceipt},{row.SystemAmount},{row.Difference},{row.Status},{Escape(row.Notes)}");
            }
        }
        else
        {
            sb.AppendLine("System Receipt,Date,Plate,Amount,Golis Reference,Status");
            foreach (var row in SystemOnlyRows)
            {
                sb.AppendLine($"{row.ReceiptNumber},{row.Date:yyyy-MM-dd},{row.Plate},{row.Amount},{row.GolisReference},{row.Status}");
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"GolisAudit_{audit.Id}_{type}.csv");
    }

    private void ReconcileAudit(int auditId)
    {
        var audit = _context.GolisAudits
            .Include(a => a.GolisTransactions)
            .First(a => a.Id == auditId);

        var golisTxs = audit.GolisTransactions.ToList();
        var fromUtc = AppTime.GetUtcDayRange(audit.AuditPeriodFrom).StartUtc;
        var toUtc = AppTime.GetUtcDayRange(audit.AuditPeriodTo).EndUtc;

        var systemPayments = _context.Payments
            .AsNoTracking()
            .Where(p => p.IsPaid && !p.IsReverted && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
            .ToList();

        // Detect duplicates within the audit
        var referenceCounts = golisTxs
            .GroupBy(t => t.GolisTransactionReference.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var tx in golisTxs)
        {
            tx.IsDuplicate = referenceCounts.GetValueOrDefault(tx.GolisTransactionReference.Trim()) > 1;
        }

        // Reset matching state
        foreach (var tx in golisTxs)
        {
            tx.MatchedPaymentId = null;
            tx.MatchedReceiptNumber = null;
            tx.MatchedSystemAmount = null;
            tx.ReconciliationStatus = GolisReconciliationStatus.NeedsReview;
        }

        var usedPaymentIds = new HashSet<int>();

        foreach (var tx in golisTxs.OrderBy(t => t.Id))
        {
            if (tx.IsDuplicate)
            {
                tx.ReconciliationStatus = GolisReconciliationStatus.Duplicate;
                continue;
            }

            // Strongest match: Golis reference == Payment.TransactionId
            var matchByReference = systemPayments
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.TransactionId) &&
                                     p.TransactionId.Trim().Equals(tx.GolisTransactionReference.Trim(), StringComparison.OrdinalIgnoreCase) &&
                                     !usedPaymentIds.Contains(p.Id));

            if (matchByReference != null)
            {
                SetMatch(tx, matchByReference);
                usedPaymentIds.Add(matchByReference.Id);
                continue;
            }

            // Fallback: same amount and date (within same day)
            var candidate = systemPayments
                .Where(p => !usedPaymentIds.Contains(p.Id))
                .FirstOrDefault(p =>
                    p.Amount == tx.Amount &&
                    AppTime.ToLocal(p.PaidAt).Date == tx.TransactionDate.Date);

            if (candidate != null)
            {
                SetMatch(tx, candidate);
                usedPaymentIds.Add(candidate.Id);
                continue;
            }

            tx.ReconciliationStatus = GolisReconciliationStatus.NotInSystem;
        }

        // System-only: payments not matched to any Golis transaction
        var matchedPaymentIds = golisTxs
            .Where(t => t.MatchedPaymentId.HasValue)
            .Select(t => t.MatchedPaymentId!.Value)
            .ToHashSet();

        var systemOnlyPayments = systemPayments.Where(p => !matchedPaymentIds.Contains(p.Id)).ToList();

        // Update audit totals
        audit.TotalGolisTransactions = golisTxs.Count;
        audit.TotalGolisAmount = golisTxs.Sum(t => t.Amount);
        audit.TotalSystemTransactions = systemPayments.Count;
        audit.TotalSystemAmount = systemPayments.Sum(p => p.Amount);
        audit.MatchedCount = golisTxs.Count(t => t.ReconciliationStatus == GolisReconciliationStatus.Matched);
        audit.MatchedAmount = golisTxs.Where(t => t.ReconciliationStatus == GolisReconciliationStatus.Matched).Sum(t => t.Amount);
        audit.NotInSystemCount = golisTxs.Count(t => t.ReconciliationStatus == GolisReconciliationStatus.NotInSystem);
        audit.NotInSystemAmount = golisTxs.Where(t => t.ReconciliationStatus == GolisReconciliationStatus.NotInSystem).Sum(t => t.Amount);
        audit.AmountMismatchCount = golisTxs.Count(t => t.ReconciliationStatus == GolisReconciliationStatus.AmountMismatch);
        audit.AmountMismatchAmount = golisTxs.Where(t => t.ReconciliationStatus == GolisReconciliationStatus.AmountMismatch).Sum(t => t.Amount);
        audit.DuplicateCount = golisTxs.Count(t => t.ReconciliationStatus == GolisReconciliationStatus.Duplicate);
        audit.SystemOnlyCount = systemOnlyPayments.Count;
        audit.SystemOnlyAmount = systemOnlyPayments.Sum(p => p.Amount);
        audit.Difference = audit.TotalGolisAmount - audit.TotalSystemAmount;

        _context.SaveChanges();
    }

    private void SetMatch(GolisTransaction tx, Payment payment)
    {
        tx.MatchedPaymentId = payment.Id;
        tx.MatchedReceiptNumber = payment.InvoiceNumber;
        tx.MatchedSystemAmount = payment.Amount;

        if (payment.Amount == tx.Amount)
            tx.ReconciliationStatus = GolisReconciliationStatus.Matched;
        else
            tx.ReconciliationStatus = GolisReconciliationStatus.AmountMismatch;
    }

    private void LoadAudit(int id)
    {
        Audit = _context.GolisAudits
            .Include(a => a.GolisTransactions)
            .ThenInclude(t => t.MatchedPayment)
            .ThenInclude(p => p!.Vehicle)
            .Include(a => a.CreatedByUser)
            .Include(a => a.FinalizedByUser)
            .FirstOrDefault(a => a.Id == id);

        if (Audit == null)
            return;

        ReconcileAudit(Audit.Id);

        TotalGolisTransactions = Audit.TotalGolisTransactions;
        TotalGolisAmount = Audit.TotalGolisAmount;
        TotalSystemTransactions = Audit.TotalSystemTransactions;
        TotalSystemAmount = Audit.TotalSystemAmount;
        MatchedCount = Audit.MatchedCount;
        MatchedAmount = Audit.MatchedAmount;
        NotInSystemCount = Audit.NotInSystemCount;
        NotInSystemAmount = Audit.NotInSystemAmount;
        AmountMismatchCount = Audit.AmountMismatchCount;
        AmountMismatchAmount = Audit.AmountMismatchAmount;
        DuplicateCount = Audit.DuplicateCount;
        SystemOnlyCount = Audit.SystemOnlyCount;
        SystemOnlyAmount = Audit.SystemOnlyAmount;
        Difference = Audit.Difference;

        var query = Audit.GolisTransactions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
        {
            if (Enum.TryParse<GolisReconciliationStatus>(StatusFilter, out var status))
                query = query.Where(t => t.ReconciliationStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            query = query.Where(t =>
                (t.GolisTransactionReference?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.MobileNumber?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.Description?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (t.MatchedReceiptNumber?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        GolisRows = query
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => new GolisTransactionViewModel
            {
                Id = t.Id,
                Reference = t.GolisTransactionReference,
                Date = t.TransactionDate,
                Time = t.TransactionTime?.ToString("hh\\:mm") ?? "",
                Mobile = t.MobileNumber,
                Amount = t.Amount,
                Description = t.Description,
                SystemReceipt = t.MatchedReceiptNumber ?? "",
                SystemAmount = t.MatchedSystemAmount ?? 0,
                Difference = t.Amount - (t.MatchedSystemAmount ?? 0),
                Status = t.ReconciliationStatus.ToString(),
                Notes = t.Notes
            })
            .ToList();

        LoadSystemOnlyRows();
    }

    private void LoadSystemOnlyRows()
    {
        if (Audit == null)
            return;

        var fromUtc = AppTime.GetUtcDayRange(Audit.AuditPeriodFrom).StartUtc;
        var toUtc = AppTime.GetUtcDayRange(Audit.AuditPeriodTo).EndUtc;

        var systemPayments = _context.Payments
            .AsNoTracking()
            .Include(p => p.Vehicle)
            .Where(p => p.IsPaid && !p.IsReverted && p.PaidAt >= fromUtc && p.PaidAt < toUtc)
            .ToList();

        var matchedPaymentIds = Audit.GolisTransactions
            .Where(t => t.MatchedPaymentId.HasValue)
            .Select(t => t.MatchedPaymentId!.Value)
            .ToHashSet();

        SystemOnlyRows = systemPayments
            .Where(p => !matchedPaymentIds.Contains(p.Id))
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new SystemOnlyViewModel
            {
                ReceiptNumber = p.InvoiceNumber,
                Date = AppTime.ToLocal(p.PaidAt),
                Plate = p.Vehicle?.PlateNumber ?? "",
                Amount = p.Amount,
                GolisReference = "—",
                Status = "SYSTEM-ONLY"
            })
            .ToList();
    }

    private User? GetCurrentUser()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return null;

        return _context.Users.AsNoTracking().FirstOrDefault(u => u.Username == username);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    public class AuditInputModel
    {
        [Required]
        public string StatementNumber { get; set; } = string.Empty;
        [Required]
        public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-7);
        [Required]
        public DateTime ToDate { get; set; } = DateTime.Today;
        public decimal StatementTotal { get; set; }
        public int StatementTransactionCount { get; set; }
        public string? Notes { get; set; }
    }

    public class GolisTransactionInputModel
    {
        [Required]
        public string Reference { get; set; } = string.Empty;
        [Required]
        public DateTime Date { get; set; }
        public TimeSpan? Time { get; set; }
        public string? MobileNumber { get; set; }
        [Required]
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
    }

    public class GolisTransactionViewModel
    {
        public int Id { get; set; }
        public string Reference { get; set; } = "";
        public DateTime Date { get; set; }
        public string Time { get; set; } = "";
        public string? Mobile { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string SystemReceipt { get; set; } = "";
        public decimal SystemAmount { get; set; }
        public decimal Difference { get; set; }
        public string Status { get; set; } = "";
        public string? Notes { get; set; }
    }

    public class SystemOnlyViewModel
    {
        public string ReceiptNumber { get; set; } = "";
        public DateTime Date { get; set; }
        public string Plate { get; set; } = "";
        public decimal Amount { get; set; }
        public string GolisReference { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
