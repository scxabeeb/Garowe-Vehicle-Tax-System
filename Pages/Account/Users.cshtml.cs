using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

[Authorize(Roles = "Admin")]
public class UsersModel : PageModel
{
    private readonly AppDbContext _context;

    private static readonly Dictionary<string, string[]> PermissionPageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard.view"] = ["Dashboard"],
        ["vehicle.view"] = ["Vehicles"],
        ["cartype.view"] = ["Car Types"],
        ["movement.view"] = ["Movements"],
        ["tax.view"] = ["Tax Amounts"],
        ["receipt.view"] = ["Receipt References"],
        ["payment.view"] = ["Receipt References"],
        ["payment.create"] = ["Collect Tax"],
        ["reports.view"] = ["Reports", "Transactions"],
        ["user.view"] = ["Users"],
        ["user.create"] = ["Register User"],
        ["user.security"] = ["User Security"],
        ["user.permissions"] = ["Permission Management"]
    };

    public UsersModel(AppDbContext context)
    {
        _context = context;
    }

    public List<UserAccessView> Users { get; set; } = new();

    public class UserAccessView
    {
        public User User { get; set; } = new();
        public string VisiblePagesText { get; set; } = "No page access assigned";
    }

    public void OnGet()
    {
        Users = _context.Users
            .OrderBy(u => u.Username)
            .Select(u => new UserAccessView
            {
                User = u,
                VisiblePagesText = BuildVisiblePagesText(u)
            })
            .ToList();
    }

    private static string BuildVisiblePagesText(User user)
    {
        if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            return "All pages (Admin)";

        var grantedPermissions = (user.Permissions ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p));

        var pages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in grantedPermissions)
        {
            if (PermissionPageMap.TryGetValue(permission, out var mappedPages))
            {
                foreach (var page in mappedPages)
                {
                    pages.Add(page);
                }
            }
        }

        if (pages.Count == 0)
            return "No page access assigned";

        return string.Join(", ", pages);
    }

    public IActionResult OnPostDelete(int id)
    {
        var user = _context.Users.FirstOrDefault(u => u.Id == id);
        if (user == null)
            return RedirectToPage();

        // Check if user is used as Collector in any Payment
        bool hasPayments = _context.Payments.Any(p => p.CollectorId == id);

        if (hasPayments)
        {
            TempData["Error"] = "This user cannot be deleted because they have payments recorded.";
            return RedirectToPage();
        }

        _context.Users.Remove(user);
        _context.SaveChanges();

        return RedirectToPage();
    }

    public IActionResult OnPostToggleActive(int id)
    {
        var user = _context.Users.FirstOrDefault(u => u.Id == id);
        if (user == null)
            return RedirectToPage();

        // Prevent locking the currently logged-in admin from this screen.
        if (!user.IsLocked && string.Equals(User.Identity?.Name, user.Username, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "You cannot set your own account to inactive from this page.";
            return RedirectToPage();
        }

        user.IsLocked = !user.IsLocked;
        _context.SaveChanges();

        TempData["Message"] = user.IsLocked
            ? $"User '{user.Username}' is now inactive."
            : $"User '{user.Username}' is now active.";

        return RedirectToPage();
    }
}
