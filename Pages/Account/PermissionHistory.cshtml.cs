using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;

namespace VehicleTax.Web.Pages.Account;

/// <summary>Permission change history — who changed what permission, for which
/// role or user, previous → new value, and when.</summary>
[HasPermission(AppPermissions.Keys.RolesManagePermissions)]
public class PermissionHistoryModel : PageModel
{
    private readonly AppDbContext _context;

    public PermissionHistoryModel(AppDbContext context) { _context = context; }

    public enum TargetFilter { All = 0, Roles = 1, Users = 2 }

    [BindProperty(SupportsGet = true)] public TargetFilter Target { get; set; } = TargetFilter.All;
    [BindProperty(SupportsGet = true)] public string? Permission { get; set; }
    [BindProperty(SupportsGet = true)] public string? ChangedBy { get; set; }

    public List<AuditRow> Rows { get; set; } = new();
    public string? Message { get; private set; }

    public class AuditRow
    {
        public DateTime When { get; set; }
        public string? ByUser { get; set; }
        public string TargetType { get; set; } = "";
        public string Target { get; set; } = "";
        public string Permission { get; set; } = "";
        public string Module { get; set; } = "";
        public string Page { get; set; } = "";
        public string OldValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public string? Details { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? message = null)
    {
        Message = message;

        var query = _context.PermissionAuditLogs
            .Include(l => l.ByUser)
            .AsQueryable();

        if (Target == TargetFilter.Roles) query = query.Where(l => l.TargetType == "Role");
        if (Target == TargetFilter.Users) query = query.Where(l => l.TargetType == "User");

        if (!string.IsNullOrWhiteSpace(Permission))
            query = query.Where(l => l.Permission.Contains(Permission.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(ChangedBy))
            query = query.Where(l => l.ByUser!.Username.Contains(ChangedBy.Trim(), StringComparison.OrdinalIgnoreCase));

        var list = await query
            .OrderByDescending(l => l.Id)
            .Take(500)
            .ToListAsync();

        foreach (var l in list)
        {
            Rows.Add(new AuditRow
            {
                When = l.ActionAt,
                ByUser = l.ByUser?.FullName ?? l.ByUser?.Username,
                TargetType = l.TargetType,
                Target = l.Target,
                Permission = l.Permission,
                Module = AppPermissions.Locate(l.Permission).Module,
                Page = AppPermissions.Locate(l.Permission).Page,
                OldValue = l.OldValue,
                NewValue = l.NewValue,
                Details = l.Details
            });
        }

        return Page();
    }
}