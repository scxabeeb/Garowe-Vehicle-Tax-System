using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;

namespace VehicleTax.Web.Pages.RevenueAccounts;

public class EditModel : PageModel
{
    private readonly AppDbContext _context;

    public EditModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public RevenueAccount? Account { get; set; }

    private bool HasPermission(string permission)
    {
        return User.IsInRole("Admin") || User.HasClaim("permission", permission);
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!HasPermission(Permissions.RevenueAccountEdit))
            return Forbid();

        Account = await _context.RevenueAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (Account == null)
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!HasPermission(Permissions.RevenueAccountEdit))
            return Forbid();

        if (Account == null || Account.Id != id)
        {
            return NotFound();
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(Account.AccountCode))
        {
            ModelState.AddModelError("Account.AccountCode", "Account Code is required.");
        }

        if (string.IsNullOrWhiteSpace(Account.AccountName))
        {
            ModelState.AddModelError("Account.AccountName", "Account Name is required.");
        }

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Account.AccountCode) || string.IsNullOrWhiteSpace(Account.AccountName))
        {
            return Page();
        }

        // Check for duplicate AccountCode (excluding current record)
        bool codeExists = await _context.RevenueAccounts
            .AnyAsync(r => r.AccountCode.Trim().ToLower() == Account.AccountCode.Trim().ToLower()
                           && r.Id != Account.Id);

        if (codeExists)
        {
            ModelState.AddModelError("Account.AccountCode", "This Account Code already exists.");
            return Page();
        }

        var existing = await _context.RevenueAccounts.FirstOrDefaultAsync(r => r.Id == Account.Id);
        if (existing == null)
            return NotFound();

        existing.AccountCode = Account.AccountCode.Trim();
        existing.AccountName = Account.AccountName.Trim();
        existing.Description = Account.Description;
        existing.IsActive = Account.IsActive;
        // Preserve CreatedAt

        await _context.SaveChangesAsync();

        TempData["Success"] = "Revenue account updated successfully.";
        return RedirectToPage("Index");
    }
}
