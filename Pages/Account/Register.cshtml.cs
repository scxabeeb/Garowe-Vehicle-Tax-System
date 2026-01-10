using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;

namespace VehicleTax.Web.Pages.Account;

[Authorize(Roles = "Admin")]
public class RegisterModel : PageModel
{
    private readonly AppDbContext _context;

    public RegisterModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public string Username { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    [BindProperty]
    public string Role { get; set; } = "";

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Username)
            || string.IsNullOrWhiteSpace(Password)
            || string.IsNullOrWhiteSpace(Role))
        {
            ModelState.AddModelError("", "All fields are required");
            return Page();
        }

        if (_context.Users.Any(u => u.Username == Username))
        {
            ModelState.AddModelError("", "Username already exists");
            return Page();
        }

        var user = new User
        {
            Username = Username.Trim(),
            Password = Password.Trim(),   // later replace with hashing
            Role = Role,
            Permissions = ""              // permissions added later
        };

        _context.Users.Add(user);
        _context.SaveChanges();

        return RedirectToPage("/Account/Users");
    }
}
