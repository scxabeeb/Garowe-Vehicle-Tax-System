using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VehicleTax.Web.Services;
using K = VehicleTax.Web.Security.AppPermissions.Keys;

namespace VehicleTax.Web.Security;

/// <summary>
/// Enforces the permission catalog on every Razor page, so granting or
/// revoking a permission in Manage Permissions takes effect on the next
/// request (effective permissions are read from the database, not the
/// login cookie).
///
/// Each page folder maps to a View key (needed to open the page) and an
/// edit key (needed for changes). Specific pages and handlers can require a
/// different key. Pages not listed fall back to Dashboard.View.
/// </summary>
public sealed class PagePermissionFilter : IAsyncPageFilter
{
    private readonly IPermissionService _permissions;
    private readonly ILogger<PagePermissionFilter> _logger;

    public PagePermissionFilter(IPermissionService permissions, ILogger<PagePermissionFilter> logger)
    {
        _permissions = permissions;
        _logger = logger;
    }

    /// <summary>Pages anyone signed in (or anonymous) may use.</summary>
    private static readonly HashSet<string> Open = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/Login", "/Account/Logout", "/Account/AccessDenied",
        "/Account/ChangePassword", "/Error",
        // Every signed-in user sees the dashboard; collectors see only
        // their own figures (Pages/Index.cshtml.cs).
        "/Index",
    };

    /// <summary>Exact page (and optional handler) rules, checked first.</summary>
    private static readonly Dictionary<string, string> PageRules = new(StringComparer.OrdinalIgnoreCase)
    {
        // Dashboard / verification
        ["/Verify/Index"] = K.PaymentsView,

        // Users
        ["/Account/Users"] = K.UsersView,
        ["/Account/Users#Delete"] = K.UsersDelete,
        ["/Account/Users#ToggleActive"] = K.UsersDelete,
        ["/Account/Register"] = K.UsersCreate,
        ["/Account/EditUser"] = K.UsersEdit,
        ["/Account/UserSecurity"] = K.UsersView,
        ["/Account/UserSecurity#ChangePassword"] = K.UsersEdit,
        ["/Account/UserSecurity#ToggleLock"] = K.UsersDelete,

        // Roles & permissions
        ["/Account/ManagePermissions"] = K.RolesManagePermissions,
        ["/Account/Permissions"] = K.RolesManagePermissions,
        ["/Account/PermissionHistory"] = K.RolesView,

        // Vehicles
        ["/Vehicles/Index"] = K.VehiclesView,
        ["/Vehicles/Create"] = K.VehiclesCreate,
        ["/Vehicles/Edit"] = K.VehiclesEdit,
        ["/Vehicles/Delete"] = K.VehiclesDelete,

        // Payments
        ["/Payments/Index"] = K.PaymentsView,
        ["/Payments/Collect"] = K.PaymentsView,
        ["/Payments/Collect#GenerateInvoice"] = K.PaymentsCreate,
        ["/Payments/Collect#CollectInvoice"] = K.PaymentsApprove,
        ["/Payments/Collect#Revert"] = K.PaymentsCancel,
        ["/Payments/Receipt"] = K.PaymentsView,
        ["/Payments/Receipt#Collect"] = K.PaymentsApprove,
        ["/Payments/Receipt#CollectPreview"] = K.PaymentsApprove,
        ["/ReceiptReferences/Index"] = K.PaymentsView,
        ["/ReceiptReferences/View"] = K.PaymentsView,
        ["/ReceiptReferences/View#Cancel"] = K.PaymentsCancel,
        ["/ReceiptReferences/Create"] = K.PaymentsCreate,
        ["/ReceiptReferences/Upload"] = K.PaymentsCreate,

        // Golis audit: viewing is a report, changing it is finance work
        ["/Reports/GolisAudit"] = K.ReportsView,
        ["/Reports/GolisAudit#Export"] = K.ReportsExport,
        // Activity log shows who signed in and changed what: user admins only.
        ["/Reports/ActivityLog"] = K.UsersView,
        ["/Reports/ActivityLog#ExportCsv"] = K.UsersView,
        ["/Transactions/Index"] = K.ReportsView,
        ["/Transactions/Index#ExportFiltered"] = K.ReportsExport,
    };

    /// <summary>Folder rules: (view key, key for every other change).</summary>
    private static readonly (string Folder, string View, string Change)[] FolderRules =
    {
        ("/CarTypes/", K.SettingsView, K.SettingsEdit),
        ("/Movements/", K.SettingsView, K.SettingsEdit),
        ("/TaxAmounts/", K.SettingsView, K.SettingsEdit),
        ("/Checkpoints/", K.SettingsView, K.SettingsEdit),
        ("/RevenueAccounts/", K.SettingsView, K.SettingsEdit),
        ("/Reports/", K.ReportsView, K.FinanceManage),
        ("/Finance/", K.FinanceView, K.FinanceManage),
    };

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var required = RequiredPermission(context);
        var user = context.HttpContext.User;

        if (required != null && user.Identity?.IsAuthenticated == true &&
            !await _permissions.HasAsync(user, required))
        {
            _logger.LogInformation("Access denied: {User} lacks {Permission} for {Page}",
                user.Identity?.Name, required, context.ActionDescriptor.ViewEnginePath);
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }

    /// <summary>The permission this request needs, or null when the page is open.</summary>
    public static string? RequiredPermission(PageHandlerExecutingContext context)
    {
        var page = context.ActionDescriptor.ViewEnginePath;
        var handler = context.HandlerMethod?.Name ?? "";
        var method = context.HttpContext.Request.Method;
        return RequiredPermission(page, handler, method);
    }

    public static string? RequiredPermission(string page, string handler, string method)
    {
        if (Open.Contains(page)) return null;

        if (!string.IsNullOrEmpty(handler) && PageRules.TryGetValue($"{page}#{handler}", out var byHandler))
            return byHandler;

        var isGet = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
        var isExport = handler.Contains("Export", StringComparison.OrdinalIgnoreCase) ||
                       handler.Contains("Download", StringComparison.OrdinalIgnoreCase);

        if (PageRules.TryGetValue(page, out var byPage))
        {
            // A page's own rule covers GET and its other handlers, except
            // that changes on view-only rules need the matching edit right.
            if (isGet || byPage != K.ReportsView) return byPage;
        }

        foreach (var (folder, view, change) in FolderRules)
        {
            if (!page.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) continue;
            if (folder == "/Reports/" && isExport) return K.ReportsExport;
            if (isGet && !IsEditorPage(page)) return view;
            return change;
        }

        if (byPage != null) return byPage;

        // Unknown pages: signed-in users with dashboard access.
        return K.DashboardView;
    }

    /// <summary>Create/Edit/Delete pages need the change key even to open them.</summary>
    private static bool IsEditorPage(string page) =>
        page.EndsWith("/Create", StringComparison.OrdinalIgnoreCase) ||
        page.EndsWith("/Edit", StringComparison.OrdinalIgnoreCase) ||
        page.EndsWith("/Delete", StringComparison.OrdinalIgnoreCase) ||
        page.EndsWith("/Upload", StringComparison.OrdinalIgnoreCase);
}
