using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Account;

[Authorize(Roles = "Admin")]
public class PermissionsModel : PageModel
{
    private readonly AppDbContext _context;

    public PermissionsModel(AppDbContext context)
    {
        _context = context;
    }

    public User UserInfo { get; set; } = null!;

    // ALL system permissions (must match your menu & pages)
    public List<string> AllPermissions { get; set; } = new()
    {
        // Dashboard
        "dashboard.view",

        // Vehicles
        "vehicle.view",
        "vehicle.create",
        "vehicle.edit",
        "vehicle.delete",

        // Car Types
        "cartype.view",
        "cartype.create",
        "cartype.edit",
        "cartype.delete",

        // Movements
        "movement.view",
        "movement.create",
        "movement.edit",
        "movement.delete",

        // Tax Amounts
        "tax.view",
        "tax.create",
        "tax.edit",
        "tax.delete",

        // Receipt References
        "receipt.view",
        "receipt.upload",

        // Payments / Collection
        "payment.view",
        "payment.create",
        "payment.edit",
        "payment.delete",

        // Reports
        "reports.view",
        "reports.export",

        // Users / Accounts
        "user.view",         // Open Users list
        "user.create",       // Register new user
        "user.security",     // Lock/Unlock + Change password
        "user.permissions"   // Manage permissions
    };

    [BindProperty]
    public List<string> SelectedPermissions { get; set; } = new();

    public void OnGet(int id)
    {
        UserInfo = _context.Users.First(u => u.Id == id);

        SelectedPermissions =
            (UserInfo.Permissions ?? "")
            .Split(",", StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToList();
    }

    public IActionResult OnPost(int id)
    {
        var user = _context.Users.First(u => u.Id == id);

        user.Permissions = string.Join(",", SelectedPermissions);

        _context.SaveChanges();

        TempData["Message"] = "Permissions updated successfully";
        return RedirectToPage("/Account/Users");
    }
}
