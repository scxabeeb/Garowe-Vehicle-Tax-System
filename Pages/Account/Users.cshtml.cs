using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Security;
using VehicleTax.Web.Services;
using System.Security.Claims;

[Authorize] // access by permission (Security/PagePermissionFilter)
public class UsersModel : PageModel
{
    private readonly AppDbContext _context;

    private readonly IPermissionService _permissions;

    public UsersModel(AppDbContext context, IPermissionService permissions)
    {
        _context = context;
        _permissions = permissions;
    }

    public List<UserAccessView> Users { get; set; } = new();
    public HashSet<string> MyPermissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public class UserAccessView
    {
        public User User { get; set; } = new();
        /// <summary>Number of permissions the user effectively holds.</summary>
        public int PermissionCount { get; set; }
        /// <summary>Modules (name, icon) the user can open.</summary>
        public List<(string Name, string Icon)> Modules { get; set; } = new();
        public bool HasOverrides { get; set; }
    }

    public async Task OnGetAsync()
    {
        MyPermissions = await _permissions.GetEffectiveAsync(User);
        var users = await _context.Users
            .Include(u => u.Checkpoint)
            .OrderBy(u => u.Username)
            .ToListAsync();
        var withOverrides = (await _context.UserPermissionOverrides.Select(o => o.UserId).Distinct().ToListAsync()).ToHashSet();

        foreach (var u in users)
        {
            // Same resolution the pages use, for this user.
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, u.Username),
                new Claim(ClaimTypes.Role, u.Role ?? ""),
            }, "preview"));
            var effective = await _permissions.GetEffectiveAsync(principal);

            Users.Add(new UserAccessView
            {
                User = u,
                PermissionCount = effective.Count,
                HasOverrides = withOverrides.Contains(u.Id),
                Modules = AppPermissions.Modules
                    .Where(m => m.Pages.Any(p => effective.Contains(p.ViewKey)))
                    .Select(m => (m.Name, m.Icon))
                    .ToList(),
            });
        }
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

        if (string.Equals(User.Identity?.Name, user.Username, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToPage();
        }

        _context.UserPermissionOverrides.RemoveRange(_context.UserPermissionOverrides.Where(o => o.UserId == id));
        _context.Users.Remove(user);
        _context.SaveChanges();

        TempData["Message"] = $"User '{user.Username}' was deleted.";
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
