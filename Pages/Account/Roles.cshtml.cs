using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Account;

/// <summary>
/// Create and manage roles. Roles created here appear in the Role dropdown
/// on Create / Edit User and in Manage Permissions.
/// </summary>
[Authorize] // access by permission (Security/PagePermissionFilter)
public class RolesModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IPermissionService _permissions;

    public RolesModel(AppDbContext context, IPermissionService permissions)
    {
        _context = context;
        _permissions = permissions;
    }

    public record Row(Role Role, int Users, int Permissions);

    public List<Row> Rows { get; set; } = new();
    public HashSet<string> My { get; set; } = new();

    // Create
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? CopyFrom { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var name = Clean(Name);
        var error = await ValidateNameAsync(name, null);
        if (error != null) return Back(error, isError: true);

        _context.Roles.Add(new Role
        {
            Name = name,
            Description = Cut(Description?.Trim(), 250),
            CreatedBy = User.Identity?.Name,
        });

        // Optionally start with another role's permissions.
        if (!string.IsNullOrWhiteSpace(CopyFrom))
        {
            var grants = await _permissions.GetRoleGrantsAsync(CopyFrom);
            foreach (var key in grants)
                _context.RolePermissions.Add(new RolePermission { Role = name, Permission = key });
        }

        await _context.SaveChangesAsync();
        _permissions.Invalidate();
        return Back($"Role '{name}' was created. It is now in the Role list when creating users.");
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, string? name, string? description)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return Back("Role not found.", isError: true);

        var newName = Clean(name);
        if (!string.Equals(newName, role.Name, StringComparison.Ordinal))
        {
            if (Role.IsProtected(role.Name)) return Back($"'{role.Name}' cannot be renamed.", isError: true);
            var error = await ValidateNameAsync(newName, role.Id);
            if (error != null) return Back(error, isError: true);

            // Users and permission grants refer to the role by name.
            var old = role.Name;
            await using var tx = await _context.Database.BeginTransactionAsync();
            foreach (var u in await _context.Users.Where(u => u.Role == old).ToListAsync()) u.Role = newName;
            foreach (var g in await _context.RolePermissions.Where(g => g.Role == old).ToListAsync()) g.Role = newName;
            role.Name = newName;
            role.Description = Cut(description?.Trim(), 250);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            _permissions.Invalidate();
            return Back($"Role '{old}' was renamed to '{newName}'.");
        }

        role.Description = Cut(description?.Trim(), 250);
        await _context.SaveChangesAsync();
        return Back($"Role '{role.Name}' was updated.");
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return Back("Role not found.", isError: true);
        if (Role.IsProtected(role.Name)) return Back($"'{role.Name}' cannot be deactivated.", isError: true);

        role.IsActive = !role.IsActive;
        await _context.SaveChangesAsync();
        return Back(role.IsActive
            ? $"Role '{role.Name}' is active again."
            : $"Role '{role.Name}' was deactivated. It is hidden from the Role list; users who have it keep it.");
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return Back("Role not found.", isError: true);
        if (Role.IsProtected(role.Name)) return Back($"'{role.Name}' cannot be deleted.", isError: true);

        var users = await _context.Users.CountAsync(u => u.Role == role.Name);
        if (users > 0)
            return Back($"'{role.Name}' still has {users} user(s). Give them another role first, or deactivate it instead.", isError: true);

        _context.RolePermissions.RemoveRange(await _context.RolePermissions.Where(g => g.Role == role.Name).ToListAsync());
        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();
        _permissions.Invalidate();
        return Back($"Role '{role.Name}' was deleted.");
    }

    private async Task LoadAsync()
    {
        My = await _permissions.GetEffectiveAsync(User);

        var roles = await _context.Roles.AsNoTracking().ToListAsync();
        var users = await _context.Users.GroupBy(u => u.Role).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key ?? "", x => x.N);

        Rows = new List<Row>();
        foreach (var r in roles)
        {
            var grants = await _permissions.GetRoleGrantsAsync(r.Name);
            Rows.Add(new Row(r, users.TryGetValue(r.Name, out var n) ? n : 0, grants.Count));
        }
        Rows = Rows.OrderBy(r => Role.IsProtected(r.Role.Name) ? 0 : 1).ThenBy(r => r.Role.Name).ToList();
    }

    private async Task<string?> ValidateNameAsync(string name, int? exceptId)
    {
        if (name.Length < 2) return "Role name must be at least 2 characters.";
        if (name.Length > 50) return "Role name must be at most 50 characters.";
        if (!Regex.IsMatch(name, @"^[\p{L}\p{N}][\p{L}\p{N} _\-]*$")) return "Use letters, numbers, spaces, - or _ only.";
        var lower = name.ToLower();
        if (await _context.Roles.AnyAsync(r => r.Id != exceptId && r.Name.ToLower() == lower))
            return "A role with this name already exists.";
        return null;
    }

    private IActionResult Back(string message, bool isError = false)
    {
        TempData[isError ? "Error" : "Message"] = message;
        return RedirectToPage();
    }

    private static string Clean(string? s) => Regex.Replace((s ?? "").Trim(), @"\s+", " ");
    private static string? Cut(string? s, int max) => string.IsNullOrEmpty(s) ? null : s.Length > max ? s[..max] : s;
}
