using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
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

    private static readonly Dictionary<string, string> Icons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Admin"] = "bi-shield-fill-check",
        ["Collector"] = "bi-cash-coin",
        ["Viewer"] = "bi-eye",
        ["Auditor"] = "bi-clipboard-check",
        ["Accountant"] = "bi-calculator",
        ["Manager"] = "bi-briefcase",
    };

    public static string IconFor(string role) => Icons.TryGetValue(role, out var i) ? i : "bi-person-badge";

    /// <summary>
    /// Active roles from Administration → Roles, plus <paramref name="keep"/>
    /// (a user's current role) even when it has been deactivated.
    /// </summary>
    public static async Task<List<RoleOption>> LoadRolesAsync(AppDbContext context, IPermissionService permissions, string? keep = null)
    {
        var roles = await context.Roles.AsNoTracking()
            .Where(r => r.IsActive || r.Name == keep)
            .OrderBy(r => r.Name)
            .ToListAsync();

        var result = new List<RoleOption>();
        foreach (var r in roles)
        {
            var grants = await permissions.GetRoleGrantsAsync(r.Name);
            result.Add(new RoleOption(r.Name, string.IsNullOrWhiteSpace(r.Description) ? "Custom role." : r.Description!,
                IconFor(r.Name), grants.Count));
        }
        // Admin first, then alphabetical.
        return result.OrderBy(r => Role.IsProtected(r.Name) ? 0 : 1).ThenBy(r => r.Name).ToList();
    }

    public static SelectList LoadCheckpoints(AppDbContext context, int? selected) =>
        new(context.Checkpoints.OrderBy(c => c.Name).ToList(), "Id", "Name", selected);
}
