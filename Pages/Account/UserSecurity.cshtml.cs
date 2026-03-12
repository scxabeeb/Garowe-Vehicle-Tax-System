using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
<<<<<<< HEAD
using VehicleTax.Web.Security;

[Authorize]
=======

[Authorize(Roles = "Admin")]
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
public class UserSecurityModel : PageModel
{
    private readonly AppDbContext _context;

    public UserSecurityModel(AppDbContext context)
    {
        _context = context;
    }

    public List<User> AllUsers { get; set; } = new();
<<<<<<< HEAD
    public User? SelectedUser { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public SecurityOperation Operation { get; set; }
=======

    public User? SelectedUser { get; set; }

    [BindProperty]
    public int Id { get; set; }
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd

    [BindProperty]
    public string NewPassword { get; set; } = "";

<<<<<<< HEAD
    public bool CanAccess(SecurityOperation op)
        => SecurityPolicy.CanAccess(op, User);

    private void LoadUsers()
    {
        AllUsers = _context.Users
            .OrderBy(u => u.Username)
            .ToList();
    }

    public IActionResult OnGet()
    {
        LoadUsers();

        if (Operation == SecurityOperation.None)
            return Page();

        if (!CanAccess(Operation))
            return Forbid();

        if (Id != null)
            SelectedUser = _context.Users.Find(Id);

        return Page();
    }

    public IActionResult OnPostToggleLock()
    {
        if (!CanAccess(SecurityOperation.AccountStatus))
            return Forbid();

        var user = _context.Users.Find(Id);
        if (user == null)
            return RedirectToPage();
=======
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
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd

        user.IsLocked = !user.IsLocked;
        _context.SaveChanges();

        TempData["Message"] = user.IsLocked
            ? "User locked successfully"
            : "User unlocked successfully";

<<<<<<< HEAD
        return RedirectToPage(new
        {
            id = user.Id,
            operation = SecurityOperation.AccountStatus
        });
    }

    public IActionResult OnPostChangePassword()
    {
        if (!CanAccess(SecurityOperation.PasswordReset))
            return Forbid();

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ModelState.AddModelError("", "Password is required");
            return Page();
        }

        var user = _context.Users.Find(Id);
        if (user == null)
            return RedirectToPage();

        user.Password = NewPassword.Trim(); // hash later
        _context.SaveChanges();

        TempData["Message"] = "Password updated successfully";

        return RedirectToPage(new
        {
            id = user.Id,
            operation = SecurityOperation.PasswordReset
        });
=======
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

        user.Password = NewPassword.Trim(); // later: hash
        _context.SaveChanges();

        TempData["Message"] = "Password changed successfully";
        return RedirectToPage(new { id = Id });
>>>>>>> 7b24e7a6af078c71b19b1652f4befd2904f706dd
    }
}
