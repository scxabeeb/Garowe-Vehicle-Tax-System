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

    public List<string> AllPermissions = new()
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

        // Receipts
        "receipt.view",
        "receipt.upload",

        // Payments / Collection
        "payment.create",
        "payment.edit",
        "payment.delete",

        // Reports
        "reports.view",
        "reports.export",

        // Users
        "user.view",
        "user.create",
        "user.edit",
        "user.delete",
        "user.permissions"
    };

    [BindProperty]
    public List<string> SelectedPermissions { get; set; } = new();

    public void OnGet(int id)
    {
        UserInfo = _context.Users.First(x => x.Id == id);

        SelectedPermissions =
            (UserInfo.Permissions ?? "")
            .Split(",", StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    public IActionResult OnPost(int id)
    {
        var user = _context.Users.First(x => x.Id == id);

        user.Permissions = string.Join(",", SelectedPermissions);

        _context.SaveChanges();

        return RedirectToPage("/Account/Users");
    }
}
