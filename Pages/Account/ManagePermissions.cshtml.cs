using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Account;

/// <summary>
/// Professional, granular RBAC permission manager. Admins control access at three
/// levels — Module (main menu), Page (sub-menu) and Action (operation) — either by
/// Role or for a specific User (with per-user Allow/Deny overrides).
///
/// Every permission is enforced server-side via PermissionService: this page is
/// only the administrative front-end.
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

    public bool ByRole { get; private set; } = true;
    public string ActiveRole { get; private set; } = "";
    public User? ActiveUser { get; private set; }

    public string FormHandler => ByRole ? "RoleSave" : "UserSave";
    public bool IsUserScope => !ByRole;
    public string ScopeTitle => ByRole
        ? $"Role Permission Matrix — {ActiveRole}"
        : $"User Permission Matrix — {ActiveUser?.FullName ?? "User"}";
    public int ActiveUserId => ActiveUser?.Id ?? 0;

    public List<string> Roles { get; private set; } = new();
    public List<User> Users { get; private set; } = new();
    public List<ModuleVM> Modules { get; private set; } = new();

    [BindProperty] public List<string> Selected { get; set; } = new();

    public string? Message { get; private set; }
    public string? ErrorMessage { get; private set; }

    public class ModuleVM
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public List<PageVM> Pages { get; set; } = new();
    }

    public class PageVM
    {
        public string Name { get; set; } = "";
        public string ViewKey { get; set; } = "";
        public List<ActionVM> Actions { get; set; } = new();
    }

    public class ActionVM
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public bool Critical { get; set; }
        /// <summary>granted / inherit / allow / deny ("" = none).</summary>
        public string State { get; set; } = "";
        /// <summary>Fixed matrix column this action maps to.</summary>
        public string Column { get; set; } = "col_manage";
        public string BadgeClass { get; set; } = "";
        public string BadgeText { get; set; } = "—";
        public string Selected { get; set; } = ""; // "inherit"/"grant"/"deny"
    }

    public async Task<IActionResult> OnGetAsync(string? role = null, int? userId = null,
        string? message = null, string? error = null)
    {
        // User scope if userId >= 0 (specific user selected OR User toggle clicked).
        // Role scope only when userId == null.
        ByRole = !userId.HasValue; // true only when userId is null (Role scope)
        ActiveRole = ByRole ? (role ?? "Admin") : "";
        Message = message;
        ErrorMessage = error;

        Roles = await _context.Users.Select(u => u.Role).Distinct().OrderBy(r => r).ToListAsync();
        Users = await _context.Users.OrderBy(u => u.Username).ToListAsync();

        if (ByRole)
        {
            ActiveRole = string.IsNullOrWhiteSpace(role) ? (Roles.FirstOrDefault() ?? "Admin") : role;
            await BuildForRoleAsync(ActiveRole);
        }
        else
        {
            var effectiveUserId = userId > 0 ? userId : (int?)null;
            ActiveUser = effectiveUserId.HasValue
                ? await _context.Users.FirstOrDefaultAsync(u => u.Id == effectiveUserId)
                : Users.FirstOrDefault();
            if (ActiveUser == null)
            {
                ByRole = true;
                ActiveRole = Roles.FirstOrDefault() ?? "Admin";
                await BuildForRoleAsync(ActiveRole);
            }
            else
            {
                await BuildForUserAsync(ActiveUser);
            }
        }
        return Page();
    }

    private async Task BuildForRoleAsync(string role)
    {
        var grants = await _permissions.GetRoleGrantsAsync(role);
        System.IO.File.AppendAllText("/tmp/vts_mp_debug.log", $"[{DateTime.UtcNow:O}] BuildForRole role={role} grants={string.Join(",", grants)}\n");
        foreach (var m in AppPermissions.Modules)
        {
            var mvm = new ModuleVM { Name = m.Name, Icon = m.Icon };
            foreach (var p in m.Pages)
            {
                var pvm = new PageVM { Name = p.Name, ViewKey = p.ViewKey };
                pvm.Actions.Add(MakeAction(p.ViewKey, "View", false,
                    grants.Contains(p.ViewKey) ? "granted" : ""));
                foreach (var a in p.Actions)
                    pvm.Actions.Add(MakeAction(a.Key, a.Label, a.Critical,
                        grants.Contains(a.Key) ? "granted" : ""));
                mvm.Pages.Add(pvm);
            }
            Modules.Add(mvm);
        }
    }

    private async Task BuildForUserAsync(User user)
    {
        var overrides = await _permissions.GetUserOverridesAsync(user.Id);
        var inherited = await _permissions.GetInheritedFromRoleAsync(user);

        foreach (var m in AppPermissions.Modules)
        {
            var mvm = new ModuleVM { Name = m.Name, Icon = m.Icon };
            foreach (var p in m.Pages)
            {
                var pvm = new PageVM { Name = p.Name, ViewKey = p.ViewKey };
                pvm.Actions.Add(MakeAction(p.ViewKey, "View", false,
                    ColumnState(p.ViewKey, overrides, inherited)));
                foreach (var a in p.Actions)
                    pvm.Actions.Add(MakeAction(a.Key, a.Label, a.Critical,
                        ColumnState(a.Key, overrides, inherited)));
                mvm.Pages.Add(pvm);
            }
            Modules.Add(mvm);
        }
    }

    private static ActionVM MakeAction(string key, string label, bool critical, string state)
    {
        var a = new ActionVM { Key = key, Label = label, Critical = critical, State = state };
        a.Column = ColumnOf(label);
        a.BadgeClass = state == "allow" ? "allow" : state == "granted" ? "granted"
                     : state == "inherit" ? "inherit" : state == "deny" ? "deny" : "";
        a.BadgeText = state == "allow" ? "Allow" : state == "granted" ? "Granted"
                    : state == "inherit" ? "Inherited" : state == "deny" ? "Deny" : "—";
        a.Selected = (state == "allow" || state == "granted") ? "grant" : state == "deny" ? "deny" : "inherit";
        return a;
    }

    private static string ColumnOf(string label) =>
        label == "View" ? "col_view" :
        label == "Create" ? "col_create" :
        label == "Edit" ? "col_edit" :
        label == "Delete" ? "col_delete" :
        (label == "Cancel" || label == "Approve") ? "col_approve" :
        (label == "Export" || label == "Print" || label == "Download") ? "col_export" : "col_manage";

    private static string ColumnState(string key, Dictionary<string, PermissionEffect> o,
        HashSet<string> inherited)
    {
        if (o.TryGetValue(key, out var eff))
            return eff == PermissionEffect.Allow ? "allow" : "deny";
        return inherited.Contains(key) ? "inherit" : "";
    }

    // ── Save: Role scope ──────────────────────────────────────────
    public async Task<IActionResult> OnPostRoleSaveAsync(string role, List<string> selected)
    {
        await _permissions.SaveRolePermissionsAsync(role, selected ?? new List<string>(), User);
        return RedirectToPage(new { role, userId = (int?)null,
            message = $"Permissions saved for role '{role}'." });
    }

    // ── Save: User scope ──────────────────────────────────────────
    public async Task<IActionResult> OnPostUserSaveAsync(int userId,
        Dictionary<string, int> overrideMap, List<string> selected)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return RedirectToPage(new { userId, error = "User not found." });

        var desired = new Dictionary<string, PermissionEffect>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in overrideMap)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || !AppPermissions.AllKeys.Contains(kv.Key)) continue;
            // 1 = clear→inherit, 2 = Allow, 3 = Deny
            if (kv.Value == 2) desired[kv.Key] = PermissionEffect.Allow;
            else if (kv.Value == 3) desired[kv.Key] = PermissionEffect.Deny;
        }
        // keep any explicitly selected Allow keys in sync (defensive)
        foreach (var k in selected ?? new List<string>())
            if (AppPermissions.AllKeys.Contains(k) && !desired.TryGetValue(k, out var e) || false)
                desired[k] = PermissionEffect.Allow;

        await _permissions.SaveUserOverridesAsync(user, desired, User);
        return RedirectToPage(new { userId,
            message = $"Permission overrides saved for '{user.Username}'." });
    }
}