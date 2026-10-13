using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using VehicleTax.Web.Data;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Account;

/// <summary>A role offered on the create / edit user forms.</summary>
public record RoleOption(string Name, string Description, string Icon, int PermissionCount);

/// <summary>Everything the shared _UserForm partial needs.</summary>
public class UserFormModel
{
    public bool IsEdit { get; set; }
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Role { get; set; } = "";
    public int? CheckpointId { get; set; }
    public bool IsLocked { get; set; }
    public List<RoleOption> Roles { get; set; } = new();
    public SelectList Checkpoints { get; set; } = null!;
    public List<string> Errors { get; set; } = new();
}

public static class UserFormSupport
{
    public const int MinPasswordLength = 6;

    private static readonly Dictionary<string, (string Description, string Icon)> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Admin"] = ("Full access to everything, including users and permissions.", "bi-shield-fill-check"),
        ["Collector"] = ("Collects tax: invoices, receipts, vehicles and their own dashboard.", "bi-cash-coin"),
        ["Viewer"] = ("Read-only: dashboard and reports.", "bi-eye"),
        ["Auditor"] = ("Reviews payments, reports and finance.", "bi-clipboard-check"),
        ["Accountant"] = ("Finance, RV and report exports.", "bi-calculator"),
        ["Manager"] = ("Oversees users, vehicles, payments and reports.", "bi-briefcase"),
    };

    /// <summary>Standard roles plus any role a user already has.</summary>
    public static async Task<List<RoleOption>> LoadRolesAsync(AppDbContext context, IPermissionService permissions)
    {
        var names = Known.Keys
            .Concat(await context.Users.Select(u => u.Role).Distinct().ToListAsync())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<RoleOption>();
        foreach (var name in names)
        {
            var grants = await permissions.GetRoleGrantsAsync(name);
            var (description, icon) = Known.TryGetValue(name, out var k) ? k : ("Custom role.", "bi-person-badge");
            result.Add(new RoleOption(name, description, icon, grants.Count));
        }
        // Standard roles first, in their usual order.
        var order = Known.Keys.ToList();
        return result
            .OrderBy(r => order.FindIndex(o => o.Equals(r.Name, StringComparison.OrdinalIgnoreCase)) is var i && i >= 0 ? i : 100)
            .ThenBy(r => r.Name)
            .ToList();
    }

    public static SelectList LoadCheckpoints(AppDbContext context, int? selected) =>
        new(context.Checkpoints.OrderBy(c => c.Name).ToList(), "Id", "Name", selected);
}
