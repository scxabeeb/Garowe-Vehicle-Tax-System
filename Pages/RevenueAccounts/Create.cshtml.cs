using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;

namespace VehicleTax.Web.Pages.RevenueAccounts;

public class CreateModel : PageModel
{
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public RevenueAccount Account { get; set; } = new();

    private bool HasPermission(string permission)
    {
        return User.IsInRole("Admin") || User.HasClaim("permission", permission);
    }

    public IActionResult OnGet()
    {
        if (!HasPermission(Permissions.RevenueAccountCreate))
            return Forbid();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!HasPermission(Permissions.RevenueAccountCreate))
            return Forbid();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(Account.AccountCode))
        {
            ModelState.AddModelError("Account.AccountCode", "Account Code is required.");
        }

        if (string.IsNullOrWhiteSpace(Account.AccountName))
        {
            ModelState.AddModelError("Account.AccountName", "Account Name is required.");
        }

        // Check for duplicate AccountCode
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(Account.AccountCode) || string.IsNullOrWhiteSpace(Account.AccountName))
        {
            return Page();
        }

        bool codeExists = await _context.RevenueAccounts
            .AnyAsync(r => r.AccountCode.Trim().ToLower() == Account.AccountCode.Trim().ToLower());

        if (codeExists)
        {
            ModelState.AddModelError("Account.AccountCode", "This Account Code already exists.");
            return Page();
        }

        Account.AccountCode = Account.AccountCode.Trim();
        Account.AccountName = Account.AccountName.Trim();

        Account.IsActive = true;
        Account.CreatedAt = DateTime.UtcNow;

        _context.RevenueAccounts.Add(Account);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Revenue account created successfully.";
        return RedirectToPage("Index");
    }
}
