using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using Microsoft.AspNetCore.Identity;

[Authorize(Roles = "Admin")]
public class UserSecurityModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserSecurityModel(AppDbContext context, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public List<User> AllUsers { get; set; } = new();

    public User? SelectedUser { get; set; }

    [BindProperty]
    public int Id { get; set; }

    [BindProperty]
    public string NewPassword { get; set; } = "";

    private void LoadUsers()
    {
        AllUsers = _context.Users
                           .OrderBy(u => u.Username)
                           .ToList();
    }

    // GET: /Account/UserSecurity or /Account/UserSecurity?id=5
    public IActionResult OnGet(int? id)
    {
        LoadUsers();

        if (id == null)
        {
            SelectedUser = null;
            return Page();
        }

        SelectedUser = _context.Users.FirstOrDefault(u => u.Id == id.Value);
        if (SelectedUser == null)
            return RedirectToPage("/Account/Users");

        Id = id.Value;
        return Page();
    }

    // POST: Lock / Unlock
    public IActionResult OnPostToggleLock()
    {
        LoadUsers();

        var user = _context.Users.Find(Id);
        if (user == null)
            return RedirectToPage("/Account/UserSecurity");

        if (!user.IsLocked && string.Equals(User.Identity?.Name, user.Username, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Message"] = "You cannot set your own account to inactive from this page.";
            return RedirectToPage(new { id = Id });
        }

        user.IsLocked = !user.IsLocked;
        _context.SaveChanges();

        TempData["Message"] = user.IsLocked
            ? "User set to inactive successfully"
            : "User set to active successfully";

        return RedirectToPage(new { id = Id });
    }

    // POST: Change Password
    public IActionResult OnPostChangePassword()
    {
        LoadUsers();

        var user = _context.Users.Find(Id);
        if (user == null)
            return RedirectToPage("/Account/UserSecurity");

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ModelState.AddModelError("", "Password cannot be empty");
            SelectedUser = user;
            return Page();
        }

        user.Password = _passwordHasher.HashPassword(user, NewPassword.Trim());
        _context.SaveChanges();

        TempData["Message"] = "Password changed successfully";
        return RedirectToPage(new { id = Id });
    }
}
