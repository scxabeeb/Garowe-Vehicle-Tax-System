using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Account;

/// <summary>
/// Permission manager. Grants permissions to a Role (switch per permission),
/// or overrides them for one User (Inherit / Allow / Deny per permission).
/// Every permission is listed once per module. Changes are enforced on the
/// next request by PagePermissionFilter and the menu.
/// </summary>
[HasPermission(AppPermissions.Keys.RolesManagePermissions)]
public class ManagePermissionsModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IPermissionService _permissions;

    public ManagePermissionsModel(AppDbContext context, IPermissionService permissions)
    {
        _context = context;
        _permissions = permissions;
    }

    /// <summary>Roles offered even before any user has them.</summary>
    public static readonly string[] StandardRoles = { "Admin", "Collector", "Viewer", "Auditor", "Accountant", "Manager" };

    /// <summary>Plain-language label for every permission key.</summary>
    public static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        [AppPermissions.Keys.DashboardView] = "Open the dashboard",
        [AppPermissions.Keys.UsersView] = "View users",
        [AppPermissions.Keys.UsersCreate] = "Create users",
        [AppPermissions.Keys.UsersEdit] = "Edit users & reset passwords",
        [AppPermissions.Keys.UsersDelete] = "Delete, lock & unlock users",
        [AppPermissions.Keys.RolesView] = "View permission history",
        [AppPermissions.Keys.RolesEdit] = "Assign roles",
        [AppPermissions.Keys.RolesManagePermissions] = "Manage permissions",
        [AppPermissions.Keys.VehiclesView] = "View vehicles",
        [AppPermissions.Keys.VehiclesCreate] = "Register vehicles",
        [AppPermissions.Keys.VehiclesEdit] = "Edit vehicles",
        [AppPermissions.Keys.VehiclesDelete] = "Delete vehicles",
        [AppPermissions.Keys.PaymentsView] = "View payments, receipts & verify plates",
        [AppPermissions.Keys.PaymentsCreate] = "Generate invoices",
        [AppPermissions.Keys.PaymentsEdit] = "Edit payments",
        [AppPermissions.Keys.PaymentsCancel] = "Cancel payments",
        [AppPermissions.Keys.PaymentsApprove] = "Collect / approve invoices",
        [AppPermissions.Keys.PaymentsPrint] = "Print receipts",
        [AppPermissions.Keys.ReportsView] = "View reports",
        [AppPermissions.Keys.ReportsExport] = "Export reports (CSV / Excel)",
        [AppPermissions.Keys.ReportsPrint] = "Print reports",
        [AppPermissions.Keys.ReportsDownload] = "Download reports",
        [AppPermissions.Keys.SettingsView] = "View setup (car types, traffida, tax, checkpoints)",
        [AppPermissions.Keys.SettingsEdit] = "Change setup",
        [AppPermissions.Keys.FinanceView] = "View finance & RV",
        [AppPermissions.Keys.FinanceManage] = "Manage RV, FMIS & Golis audits",
    };

    public bool ByRole { get; private set; } = true;
    public string ActiveRole { get; private set; } = "";
    public User? ActiveUser { get; private set; }
    public List<string> Roles { get; private set; } = new();
    public List<User> Users { get; private set; } = new();
    public List<ModuleRow> Modules { get; private set; } = new();
    public bool ActiveIsAdmin => string.Equals(ByRole ? ActiveRole : ActiveUser?.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    public string? Message { get; private set; }
    public string? ErrorMessage { get; private set; }

    public class ModuleRow
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public List<PermissionRow> Permissions { get; set; } = new();
    }

    public class PermissionRow
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        /// <summary>The View key this permission depends on (null for View keys).</summary>
        public string? RequiresKey { get; set; }
        public bool Critical { get; set; }
        /// <summary>Role scope: granted to the role. User scope: the role gives it.</summary>
        public bool RoleHas { get; set; }
        /// <summary>User scope: "inherit", "allow" or "deny".</summary>
        public string Override { get; set; } = "inherit";
        /// <summary>Always granted to Admins (lock-out protection).</summary>
        public bool Locked { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? role = null, int? userId = null,
        string? message = null, string? error = null)
    {
        Message = message;
        ErrorMessage = error;
        ByRole = !userId.HasValue;

        var usedRoles = await _context.Users.Select(u => u.Role).Distinct().ToListAsync();
        var grantedRoles = await _context.RolePermissions.Select(r => r.Role).Distinct().ToListAsync();
        Roles = StandardRoles.Concat(usedRoles).Concat(grantedRoles)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => Array.IndexOf(StandardRoles, r) is var i && i >= 0 ? i : 100)
            .ThenBy(r => r)
            .ToList();
        Users = await _context.Users.OrderBy(u => u.Username).ToListAsync();

        if (!ByRole)
        {
            ActiveUser = userId > 0
                ? await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
                : Users.FirstOrDefault();
            if (ActiveUser == null) ByRole = true;
        }

        if (ByRole)
        {
            ActiveRole = Roles.FirstOrDefault(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase))
                         ?? Roles.FirstOrDefault() ?? "Admin";
            var grants = await _permissions.GetRoleGrantsAsync(ActiveRole);
            Modules = BuildModules(key => grants.Contains(key), _ => "inherit",
                string.Equals(ActiveRole, "Admin", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            var inherited = await _permissions.GetRoleGrantsAsync(ActiveUser!.Role ?? "");
            var overrides = await _permissions.GetUserOverridesAsync(ActiveUser.Id);
            Modules = BuildModules(
                key => inherited.Contains(key),
                key => overrides.TryGetValue(key, out var e) ? (e == PermissionEffect.Allow ? "allow" : "deny") : "inherit",
                string.Equals(ActiveUser.Role, "Admin", StringComparison.OrdinalIgnoreCase));
        }
        return Page();
    }

    private static List<ModuleRow> BuildModules(Func<string, bool> roleHas, Func<string, string> overrideOf, bool isAdmin)
    {
        var modules = new List<ModuleRow>();
        foreach (var m in AppPermissions.Modules)
        {
            var row = new ModuleRow { Name = m.Name, Icon = m.Icon };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in m.Pages)
            {
                void Add(string key, string? requires, bool critical)
                {
                    if (!seen.Add(key)) return;
                    row.Permissions.Add(new PermissionRow
                    {
                        Key = key,
                        Label = Labels.TryGetValue(key, out var l) ? l : key,
                        RequiresKey = requires,
                        Critical = critical,
                        RoleHas = roleHas(key),
                        Override = overrideOf(key),
                        Locked = isAdmin && PermissionService.AdminFloor.Contains(key, StringComparer.OrdinalIgnoreCase),
                    });
                }
                Add(p.ViewKey, null, false);
                foreach (var a in p.Actions) Add(a.Key, p.ViewKey, a.Critical);
            }
            modules.Add(row);
        }
        return modules;
    }

    // ── Save: Role scope (selected = every granted key) ───────────
    public async Task<IActionResult> OnPostRoleSaveAsync(string role, List<string> selected)
    {
        if (string.IsNullOrWhiteSpace(role))
            return RedirectToPage(new { error = "Choose a role." });

        await _permissions.SaveRolePermissionsAsync(role, selected ?? new List<string>(), User);
        return RedirectToPage(new { role, message = $"Permissions saved for role '{role}'." });
    }

    // ── Save: User scope (overrideMap = every Allow/Deny choice) ──
    public async Task<IActionResult> OnPostUserSaveAsync(int userId, Dictionary<string, int> overrideMap)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return RedirectToPage(new { userId, error = "User not found." });

        // The form always sends the complete set of Allow (2) / Deny (3)
        // choices; anything not sent goes back to inheriting from the role.
        var desired = new Dictionary<string, PermissionEffect>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in overrideMap ?? new Dictionary<string, int>())
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || !AppPermissions.AllKeys.Contains(kv.Key)) continue;
            if (kv.Value == 2) desired[kv.Key] = PermissionEffect.Allow;
            else if (kv.Value == 3) desired[kv.Key] = PermissionEffect.Deny;
        }

        await _permissions.SaveUserOverridesAsync(user, desired, User);
        return RedirectToPage(new { userId, message = $"Permissions saved for '{user.Username}'." });
    }
}
