using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;

namespace VehicleTax.Web.Pages.RevenueAccounts;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    // 🔍 Search by AccountCode
    [BindProperty(SupportsGet = true)]
    public string? SearchCode { get; set; }

    // 🔍 Search by AccountName
    [BindProperty(SupportsGet = true)]
    public string? SearchName { get; set; }

    // 🔍 Filter by Active/Inactive
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; } // "active", "inactive", "" (all)

    // 📄 Pagination
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }

    public List<RevenueAccount> Accounts { get; set; } = new();

    // 👮 Permission check
    private bool HasPermission(string permission)
    {
        return User.IsInRole("Admin") || User.HasClaim("permission", permission);
    }

    public IActionResult OnGet()
    {
        if (!HasPermission(Permissions.RevenueAccountView))
            return Forbid();

        IQueryable<RevenueAccount> query = _context.RevenueAccounts.AsNoTracking();

        // Search by AccountCode
        if (!string.IsNullOrWhiteSpace(SearchCode))
        {
            query = query.Where(r => EF.Functions.Like(r.AccountCode, $"%{SearchCode}%"));
        }

        // Search by AccountName
        if (!string.IsNullOrWhiteSpace(SearchName))
        {
            query = query.Where(r => EF.Functions.Like(r.AccountName, $"%{SearchName}%"));
        }

        // Filter by status
        if (!string.IsNullOrEmpty(Status))
        {
            if (Status == "active")
                query = query.Where(r => r.IsActive);
            else if (Status == "inactive")
                query = query.Where(r => !r.IsActive);
        }

        int totalCount = query.Count();
        TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

        if (TotalPages < 1)
            TotalPages = 1;

        if (PageNumber < 1)
            PageNumber = 1;

        if (PageNumber > TotalPages)
            PageNumber = TotalPages;

        Accounts = query
            .OrderBy(r => r.AccountCode)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostActivateAsync(int id)
    {
        if (!HasPermission(Permissions.RevenueAccountEdit))
            return Forbid();

        var account = await _context.RevenueAccounts.FindAsync(id);
        if (account == null)
            return NotFound();

        account.IsActive = true;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Revenue account activated successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        if (!HasPermission(Permissions.RevenueAccountEdit))
            return Forbid();

        var account = await _context.RevenueAccounts.FindAsync(id);
        if (account == null)
            return NotFound();

        account.IsActive = false;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Revenue account deactivated successfully.";
        return RedirectToPage();
    }
}
