namespace VehicleTax.Web.Security;

/// <summary>
/// Central catalog of every permission key in the system, organised hierarchically:
/// Module (main menu) → Page (sub-menu) → Action (operation).
///
/// Keys follow the "Module.Action" convention (e.g. Vehicles.Create). A page's
/// View key gates access to the page; action keys only ever work when the user
/// also holds the page's View permission (enforced by PermissionService).
/// </summary>
public static class AppPermissions
{
    public const string SuperAdminRole = "Super Admin";

    /// <summary>Permissions that must be confirmed before revocation.</summary>
    public static readonly HashSet<string> Critical = new(StringComparer.OrdinalIgnoreCase)
    {
        Keys.UsersDelete,
        Keys.RolesDelete,
        Keys.RolesManagePermissions,
        Keys.SettingsEdit
    };

    public static class Keys
    {
        // ── Dashboard ─────────────────────────────────────────────
        public const string DashboardView = "Dashboard.View";

        // ── Users ─────────────────────────────────────────────────
        public const string UsersView = "Users.View";
        public const string UsersCreate = "Users.Create";
        public const string UsersEdit = "Users.Edit";
        public const string UsersDelete = "Users.Delete";

        // ── Roles ─────────────────────────────────────────────────
        public const string RolesView = "Roles.View";
        public const string RolesCreate = "Roles.Create";
        public const string RolesEdit = "Roles.Edit";
        public const string RolesDelete = "Roles.Delete";
        public const string RolesManagePermissions = "Roles.ManagePermissions";

        // ── Vehicles ──────────────────────────────────────────────
        public const string VehiclesView = "Vehicles.View";
        public const string VehiclesCreate = "Vehicles.Create";
        public const string VehiclesEdit = "Vehicles.Edit";
        public const string VehiclesDelete = "Vehicles.Delete";

        // ── Payments ──────────────────────────────────────────────
        public const string PaymentsView = "Payments.View";
        public const string PaymentsCreate = "Payments.Create";
        public const string PaymentsEdit = "Payments.Edit";
        public const string PaymentsCancel = "Payments.Cancel";
        public const string PaymentsApprove = "Payments.Approve";
        public const string PaymentsPrint = "Payments.Print";

        // ── Reports ───────────────────────────────────────────────
        public const string ReportsView = "Reports.View";
        public const string ReportsExport = "Reports.Export";
        public const string ReportsPrint = "Reports.Print";
        public const string ReportsDownload = "Reports.Download";

        // ── Settings (Data & Setup) ───────────────────────────────
        public const string SettingsView = "Settings.View";
        public const string SettingsEdit = "Settings.Edit";

        // ── Finance (RF / FMIS) ───────────────────────────────────
        public const string FinanceView = "Finance.View";
        public const string FinanceManage = "Finance.Manage";
    }

    public static bool IsCritical(string key) => Critical.Contains(key);

    /// <summary>An operation that may be granted on a page.</summary>
    public sealed class ActionDef
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public bool Critical { get; init; }
    }

    /// <summary>A sub-menu page inside a module, with the actions it exposes.</summary>
    public sealed class PageDef
    {
        public required string Name { get; init; }
        public required string ViewKey { get; init; }
        public string? Icon { get; init; }
        public List<ActionDef> Actions { get; } = new();
    }

    /// <summary>A main-menu module.</summary>
    public sealed class ModuleDef
    {
        public required string Name { get; init; }
        public required string Icon { get; init; }
        public List<PageDef> Pages { get; } = new();
        public IEnumerable<string> AllKeys =>
            Pages.SelectMany(p => p.Actions.Select(a => a.Key).Prepend(p.ViewKey)).Distinct();
    }

    private static readonly Lazy<List<ModuleDef>> _modules = new(BuildCatalog);
    public static List<ModuleDef> Modules => _modules.Value;

    public static readonly HashSet<string> AllKeys = new(
        Modules.SelectMany(m => m.AllKeys), StringComparer.OrdinalIgnoreCase);

    /// <summary>Lookup: permission key → (module, page, action label).</summary>
    public static (string Module, string Page, string Action) Locate(string key)
    {
        foreach (var m in Modules)
            foreach (var p in m.Pages)
            {
                if (p.ViewKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return (m.Name, p.Name, "View");
                var a = p.Actions.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (a != null) return (m.Name, p.Name, a.Label);
            }
        return ("", "", "");
    }

    private static List<ModuleDef> BuildCatalog()
    {
        return new List<ModuleDef>
        {
            new()
            {
                Name = "Dashboard", Icon = "bi-speedometer2",
                Pages =
                {
                    new PageDef { Name = "Dashboard Overview", ViewKey = Keys.DashboardView, Icon = "bi-speedometer2" }
                }
            },
            new()
            {
                Name = "Users", Icon = "bi-people",
                Pages =
                {
                    new PageDef { Name = "View Users", ViewKey = Keys.UsersView, Icon = "bi-list-ul" },
                    new PageDef { Name = "Create User", ViewKey = Keys.UsersView, Icon = "bi-person-plus",
                        Actions = { new ActionDef { Key = Keys.UsersCreate, Label = "Create" } } },
                    new PageDef { Name = "Edit User", ViewKey = Keys.UsersView, Icon = "bi-pencil-square",
                        Actions = { new ActionDef { Key = Keys.UsersEdit, Label = "Edit" } } },
                    new PageDef { Name = "Delete / Lock User", ViewKey = Keys.UsersView, Icon = "bi-shield-lock",
                        Actions = { new ActionDef { Key = Keys.UsersDelete, Label = "Delete", Critical = true } } }
                }
            },
            new()
            {
                Name = "Roles", Icon = "bi-diagram-2",
                Pages =
                {
                    new PageDef { Name = "View Roles", ViewKey = Keys.RolesView, Icon = "bi-diagram-2" },
                    new PageDef { Name = "Assign Roles", ViewKey = Keys.RolesView, Icon = "bi-person-gear",
                        Actions = { new ActionDef { Key = Keys.RolesEdit, Label = "Edit" } } },
                    new PageDef { Name = "Manage Permissions", ViewKey = Keys.RolesView, Icon = "bi-sliders",
                        Actions = { new ActionDef { Key = Keys.RolesManagePermissions, Label = "Manage", Critical = true } } }
                }
            },
            new()
            {
                Name = "Vehicles", Icon = "bi-truck",
                Pages =
                {
                    new PageDef { Name = "Vehicle List", ViewKey = Keys.VehiclesView, Icon = "bi-list-ul" },
                    new PageDef { Name = "Register Vehicle", ViewKey = Keys.VehiclesView, Icon = "bi-plus-circle",
                        Actions = { new ActionDef { Key = Keys.VehiclesCreate, Label = "Create" } } },
                    new PageDef { Name = "Vehicle Management", ViewKey = Keys.VehiclesView, Icon = "bi-pencil-square",
                        Actions =
                        {
                            new ActionDef { Key = Keys.VehiclesEdit, Label = "Edit" },
                            new ActionDef { Key = Keys.VehiclesDelete, Label = "Delete" }
                        } }
                }
            },
            new()
            {
                Name = "Payments", Icon = "bi-cash-stack",
                Pages =
                {
                    new PageDef { Name = "Collect Payment", ViewKey = Keys.PaymentsView, Icon = "bi-wallet2",
                        Actions = { new ActionDef { Key = Keys.PaymentsCreate, Label = "Create" } } },
                    new PageDef { Name = "Payment History", ViewKey = Keys.PaymentsView, Icon = "bi-journal-text",
                        Actions =
                        {
                            new ActionDef { Key = Keys.PaymentsEdit, Label = "Edit" },
                            new ActionDef { Key = Keys.PaymentsCancel, Label = "Cancel" },
                            new ActionDef { Key = Keys.PaymentsApprove, Label = "Approve" }
                        } },
                    new PageDef { Name = "Receipts & Printing", ViewKey = Keys.PaymentsView, Icon = "bi-printer",
                        Actions = { new ActionDef { Key = Keys.PaymentsPrint, Label = "Print" } } }
                }
            },
            new()
            {
                Name = "Reports", Icon = "bi-bar-chart-line",
                Pages =
                {
                    new PageDef { Name = "Reports", ViewKey = Keys.ReportsView, Icon = "bi-graph-up-arrow",
                        Actions =
                        {
                            new ActionDef { Key = Keys.ReportsExport, Label = "Export" },
                            new ActionDef { Key = Keys.ReportsPrint, Label = "Print" },
                            new ActionDef { Key = Keys.ReportsDownload, Label = "Download" }
                        } }
                }
            },
            new()
            {
                Name = "Settings", Icon = "bi-gear",
                Pages =
                {
                    new PageDef { Name = "Car Types", ViewKey = Keys.SettingsView, Icon = "bi-diagram-3" },
                    new PageDef { Name = "Movements", ViewKey = Keys.SettingsView, Icon = "bi-arrow-left-right" },
                    new PageDef { Name = "Tax Amounts", ViewKey = Keys.SettingsView, Icon = "bi-currency-dollar" },
                    new PageDef { Name = "Checkpoints", ViewKey = Keys.SettingsView, Icon = "bi-geo-alt" },
                    new PageDef { Name = "Revenue Accounts", ViewKey = Keys.SettingsView, Icon = "bi-wallet2" },
                    new PageDef { Name = "System Settings", ViewKey = Keys.SettingsView, Icon = "bi-sliders",
                        Actions = { new ActionDef { Key = Keys.SettingsEdit, Label = "Edit", Critical = true } } }
                }
            },
            new()
            {
                Name = "Finance", Icon = "bi-bank",
                Pages =
                {
                    new PageDef { Name = "Finance Dashboard", ViewKey = Keys.FinanceView, Icon = "bi-cash-stack" },
                    new PageDef { Name = "RF & FMIS", ViewKey = Keys.FinanceView, Icon = "bi-file-earmark-text",
                        Actions = { new ActionDef { Key = Keys.FinanceManage, Label = "Manage" } } }
                }
            }
        };
    }

    /// <summary>
    /// Legacy permission keys stored in User.Permissions and cookie claims
    /// ("vehicle.create", "payment.view", …) mapped onto the new catalog keys.
    /// </summary>
    public static readonly Dictionary<string, string[]> LegacyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard.view"] = [Keys.DashboardView],
        ["vehicle.view"] = [Keys.VehiclesView],
        ["vehicle.create"] = [Keys.VehiclesCreate],
        ["vehicle.edit"] = [Keys.VehiclesEdit],
        ["vehicle.delete"] = [Keys.VehiclesDelete],
        ["cartype.view"] = [Keys.SettingsView],
        ["cartype.create"] = [Keys.SettingsEdit],
        ["cartype.edit"] = [Keys.SettingsEdit],
        ["cartype.delete"] = [Keys.SettingsEdit],
        ["movement.view"] = [Keys.SettingsView],
        ["movement.create"] = [Keys.SettingsEdit],
        ["movement.edit"] = [Keys.SettingsEdit],
        ["movement.delete"] = [Keys.SettingsEdit],
        ["tax.view"] = [Keys.SettingsView],
        ["tax.create"] = [Keys.SettingsEdit],
        ["tax.edit"] = [Keys.SettingsEdit],
        ["tax.delete"] = [Keys.SettingsEdit],
        ["receipt.view"] = [Keys.PaymentsView],
        ["receipt.upload"] = [Keys.PaymentsCreate],
        ["payment.view"] = [Keys.PaymentsView],
        ["payment.create"] = [Keys.PaymentsCreate],
        ["payment.edit"] = [Keys.PaymentsEdit],
        ["payment.delete"] = [Keys.PaymentsCancel],
        ["reports.view"] = [Keys.ReportsView],
        ["reports.export"] = [Keys.ReportsExport],
        ["user.view"] = [Keys.UsersView],
        ["user.create"] = [Keys.UsersCreate],
        ["user.edit"] = [Keys.UsersEdit],
        ["user.delete"] = [Keys.UsersDelete],
        ["user.security"] = [Keys.UsersEdit],
        ["user.permissions"] = [Keys.RolesManagePermissions],
        ["revenueaccount.view"] = [Keys.SettingsView],
        ["finance.manage"] = [Keys.FinanceView, Keys.FinanceManage]
    };

    /// <summary>Maps a catalog key back to its legacy claim value for backward compatibility.</summary>
    public static readonly Dictionary<string, string> ToLegacy = new(StringComparer.OrdinalIgnoreCase)
    {
        [Keys.DashboardView] = "dashboard.view",
        [Keys.VehiclesView] = "vehicle.view",
        [Keys.VehiclesCreate] = "vehicle.create",
        [Keys.VehiclesEdit] = "vehicle.edit",
        [Keys.VehiclesDelete] = "vehicle.delete",
        [Keys.SettingsView] = "tax.view",
        [Keys.SettingsEdit] = "tax.edit",
        [Keys.PaymentsView] = "payment.view",
        [Keys.PaymentsCreate] = "payment.create",
        [Keys.PaymentsEdit] = "payment.edit",
        [Keys.PaymentsCancel] = "payment.delete",
        [Keys.ReportsView] = "reports.view",
        [Keys.ReportsExport] = "reports.export",
        [Keys.UsersView] = "user.view",
        [Keys.UsersCreate] = "user.create",
        [Keys.UsersEdit] = "user.edit",
        [Keys.UsersDelete] = "user.delete",
        [Keys.RolesView] = "user.permissions",
        [Keys.RolesManagePermissions] = "user.permissions",
        [Keys.FinanceView] = "finance.manage",
        [Keys.FinanceManage] = "finance.manage"
    };
}
