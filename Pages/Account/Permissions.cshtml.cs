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

    public class PermissionDefinition
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
    }

    public static readonly Dictionary<string, string[]> PermissionPageMap = new(StringComparer.OrdinalIgnoreCase)
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

    public static readonly List<PermissionDefinition> PermissionCatalog = new()
    {
        new() { Key = "dashboard.view", Label = "View Dashboard", Description = "Open system dashboard and KPI cards.", Group = "Dashboard" },

        new() { Key = "vehicle.view", Label = "View Vehicles", Description = "Open vehicles registry list.", Group = "Vehicles" },
        new() { Key = "vehicle.create", Label = "Create Vehicle", Description = "Register new vehicles.", Group = "Vehicles" },
        new() { Key = "vehicle.edit", Label = "Edit Vehicle", Description = "Update existing vehicle records.", Group = "Vehicles" },
        new() { Key = "vehicle.delete", Label = "Delete Vehicle", Description = "Remove vehicle records.", Group = "Vehicles" },

        new() { Key = "cartype.view", Label = "View Car Types", Description = "Open car type list.", Group = "Car Types" },
        new() { Key = "cartype.create", Label = "Create Car Type", Description = "Add new car type definitions.", Group = "Car Types" },
        new() { Key = "cartype.edit", Label = "Edit Car Type", Description = "Update car type details.", Group = "Car Types" },
        new() { Key = "cartype.delete", Label = "Delete Car Type", Description = "Remove car type definitions.", Group = "Car Types" },

        new() { Key = "movement.view", Label = "View Movements", Description = "Open movement list.", Group = "Movements" },
        new() { Key = "movement.create", Label = "Create Movement", Description = "Add new movement types.", Group = "Movements" },
        new() { Key = "movement.edit", Label = "Edit Movement", Description = "Update movement types.", Group = "Movements" },
        new() { Key = "movement.delete", Label = "Delete Movement", Description = "Remove movement types.", Group = "Movements" },

        new() { Key = "tax.view", Label = "View Tax Amounts", Description = "Open tax amount setup.", Group = "Tax Setup" },
        new() { Key = "tax.create", Label = "Create Tax Amount", Description = "Add tax amount configuration.", Group = "Tax Setup" },
        new() { Key = "tax.edit", Label = "Edit Tax Amount", Description = "Modify tax amount configuration.", Group = "Tax Setup" },
        new() { Key = "tax.delete", Label = "Delete Tax Amount", Description = "Remove tax amount configuration.", Group = "Tax Setup" },

        new() { Key = "receipt.view", Label = "View Receipt References", Description = "Open receipt references list.", Group = "Payments" },
        new() { Key = "receipt.upload", Label = "Upload Receipt References", Description = "Upload receipt references file.", Group = "Payments" },
        new() { Key = "payment.view", Label = "View Payment Tools", Description = "Access payment utility pages.", Group = "Payments" },
        new() { Key = "payment.create", Label = "Collect Tax", Description = "Open tax collection page.", Group = "Payments" },
        new() { Key = "payment.edit", Label = "Edit Payment", Description = "Modify recorded payments.", Group = "Payments" },
        new() { Key = "payment.delete", Label = "Delete Payment", Description = "Delete payment records.", Group = "Payments" },

        new() { Key = "reports.view", Label = "View Reports", Description = "Open reporting and transaction pages.", Group = "Reports" },
        new() { Key = "reports.export", Label = "Export Reports", Description = "Export reports to file formats.", Group = "Reports" },

        new() { Key = "user.view", Label = "View Users", Description = "Open users listing page.", Group = "Administration" },
        new() { Key = "user.create", Label = "Create User", Description = "Register new system users.", Group = "Administration" },
        new() { Key = "user.security", Label = "User Security", Description = "Lock, unlock, and reset user passwords.", Group = "Administration" },
        new() { Key = "user.permissions", Label = "Manage Permissions", Description = "Assign permissions to users.", Group = "Administration" }
    };

    public PermissionsModel(AppDbContext context)
    {
        _context = context;
    }

    public User UserInfo { get; set; } = null!;

    [BindProperty]
    public List<string> SelectedPermissions { get; set; } = new();

    public List<string> SelectedVisiblePages { get; set; } = new();

    public void OnGet(int id)
    {
        var user = _context.Users.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            UserInfo = new User();
            SelectedPermissions = new List<string>();
            SelectedVisiblePages = new List<string>();
            return;
        }

        UserInfo = user;

        SelectedPermissions =
            (UserInfo.Permissions ?? "")
            .Split(",", StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => PermissionCatalog.Any(p => p.Key.Equals(x, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        SelectedVisiblePages = BuildVisiblePages(SelectedPermissions);
    }

    public IActionResult OnPost(int id)
    {
        var user = _context.Users.FirstOrDefault(u => u.Id == id);
        if (user == null)
            return RedirectToPage("/Account/Users");

        var allowedPermissions = PermissionCatalog
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sanitized = (SelectedPermissions ?? new List<string>())
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p) && allowedPermissions.Contains(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p)
            .ToList();

        user.Permissions = string.Join(",", sanitized);

        _context.SaveChanges();

        TempData["Message"] = "Permissions updated successfully";
        return RedirectToPage("/Account/Users");
    }

    private static List<string> BuildVisiblePages(IEnumerable<string> permissions)
    {
        var pages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in permissions)
        {
            if (PermissionPageMap.TryGetValue(permission, out var mappedPages))
            {
                foreach (var page in mappedPages)
                {
                    pages.Add(page);
                }
            }
        }

        return pages.ToList();
    }
}
