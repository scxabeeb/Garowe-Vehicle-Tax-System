using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Finance.Rf;

/// <summary>
/// RF History — searchable list of all RF documents (Finance batches) with status,
/// FMIS status, account, totals and prepared-by. RF numbers are Finance document
/// numbers and are entirely separate from Payment Reference Numbers.
/// </summary>
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;
    public IndexModel(AppDbContext context) { _context = context; }

    public bool IsFinance { get; private set; }

    [BindProperty(SupportsGet = true)] public string? RfNumber { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? Date { get; set; }
    [BindProperty(SupportsGet = true)] public int? AccountId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? FmisStatus { get; set; }

    public List<RfRow> Rows { get; set; } = new();
    public List<RevenueAccount> Accounts { get; set; } = new();

    public class RfRow
    {
        public int Id { get; set; }
        public string RfNumber { get; set; } = "";
        public DateTime RfDate { get; set; }
        public string? Account { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PreparedBy { get; set; }
        public string Status { get; set; } = "";
        public string FmisStatus { get; set; } = "";
        public string? FmisBatchNumber { get; set; }
        public DateTime? TransferredAt { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        IsFinance = User.IsInRole("Admin") || User.HasClaim("permission", "finance.manage");
        if (!IsFinance) return RedirectToPage("/Index");

        Accounts = await _context.RevenueAccounts
            .Where(a => a.IsActive)
            .OrderBy(a => a.AccountName)
            .ToListAsync();

        var query = _context.RfDocuments
            .Include(r => r.RevenueAccount)
            .Include(r => r.PreparedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(RfNumber))
            query = query.Where(r => r.RfNumber.Contains(RfNumber.Trim()));
        if (Date.HasValue)
        {
            var d = Date.Value.Date;
            query = query.Where(r => r.RfDate >= d && r.RfDate < d.AddDays(1));
        }
        if (AccountId.HasValue && AccountId.Value > 0)
            query = query.Where(r => r.RevenueAccountId == AccountId.Value);
        if (!string.IsNullOrWhiteSpace(Status) && Enum.TryParse<RfStatus>(Status, out var st))
            query = query.Where(r => r.Status == st);
        if (!string.IsNullOrWhiteSpace(FmisStatus) && Enum.TryParse<FmisTransferStatus>(FmisStatus, out var fs))
            query = query.Where(r => r.FmisStatus == fs);

        Rows = await query
            .OrderByDescending(r => r.Id)
            .Select(r => new RfRow
            {
                Id = r.Id,
                RfNumber = r.RfNumber,
                RfDate = r.RfDate,
                Account = r.RevenueAccount == null
                    ? null
                    : r.RevenueAccount.AccountCode + " - " + r.RevenueAccount.AccountName,
                TotalTransactions = r.TotalTransactions,
                TotalAmount = r.TotalAmount,
                PreparedBy = r.PreparedBy != null
                    ? (r.PreparedBy.FullName ?? r.PreparedBy.Username)
                    : null,
                Status = r.Status.ToString(),
                FmisStatus = r.FmisStatus.ToString(),
                FmisBatchNumber = r.FmisBatchNumber,
                TransferredAt = r.TransferredAt
            })
            .ToListAsync();

        return Page();
    }
}
