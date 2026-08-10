using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;

namespace VehicleTax.Web.Pages.RevenueAccounts;

public class DeleteModel : PageModel
{
    private readonly AppDbContext _context;

    public DeleteModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public RevenueAccount? Account { get; set; }

    public string? ErrorMessage { get; set; }

    // Counts for the friendly message
    public int MovementCount { get; set; }
    public int TaxAmountCount { get; set; }
    public int PaymentCount { get; set; }

    private bool HasPermission(string permission)
    {
        return User.IsInRole("Admin") || User.HasClaim("permission", permission);
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!HasPermission(Permissions.RevenueAccountDelete))
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
        if (!HasPermission(Permissions.RevenueAccountDelete))
            return Forbid();

        var account = await _context.RevenueAccounts
            .FirstOrDefaultAsync(r => r.Id == id);

        if (account == null)
            return RedirectToPage("Index");

        // DELETE PROTECTION: Check if account is used by Movements, TaxAmounts, or Payments
        MovementCount = await _context.Movements
            .CountAsync(m => m.RevenueAccountId == account.Id);

        TaxAmountCount = await _context.TaxAmounts
            .Where(t => t.Movement != null && t.Movement.RevenueAccountId == account.Id)
            .CountAsync();

        PaymentCount = await _context.Payments
            .Where(p => p.Movement != null && p.Movement.RevenueAccountId == account.Id)
            .CountAsync();

        if (MovementCount > 0 || TaxAmountCount > 0 || PaymentCount > 0)
        {
            var details = $"{MovementCount} movement(s)";
            if (TaxAmountCount > 0)
                details += $", {TaxAmountCount} tax amount(s)";
            if (PaymentCount > 0)
                details += $", and {PaymentCount} payment(s)";

            ErrorMessage = $"This Revenue Account cannot be deleted because it is currently being used. " +
                           $"It is linked to {details}. " +
                           "We recommend deactivating the account instead.";

            Account = account;
            return Page();
        }

        _context.RevenueAccounts.Remove(account);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Revenue account deleted successfully.";
        return RedirectToPage("Index");
    }
}
