 using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VehicleTax.Web.Pages.Account;

[Authorize(Roles = "Admin")]
public class RegisterModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public RegisterModel(AppDbContext context, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [BindProperty]
    public string Username { get; set; } = "";

    [BindProperty]
    public string FullName { get; set; } = "";

    [BindProperty]
    public string Phone { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    [BindProperty]
    public string Role { get; set; } = "";

    [BindProperty]
    public int? CheckpointId { get; set; }

    public SelectList Checkpoints { get; set; } = null!;

    public void OnGet()
    {
        LoadCheckpoints();
    }

    public IActionResult OnPost()
    {
        LoadCheckpoints();

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

        if (CheckpointId.HasValue && !_context.Checkpoints.Any(c => c.Id == CheckpointId.Value))
        {
            ModelState.AddModelError("", "Selected checkpoint does not exist");
            return Page();
        }

        var user = new User
        {
            Username = Username.Trim(),
            FullName = FullName?.Trim() ?? "",
            Phone = Phone?.Trim() ?? "",
            Role = Role,
            Permissions = "",
            CheckpointId = CheckpointId
        };

        user.Password = _passwordHasher.HashPassword(user, Password.Trim());

        _context.Users.Add(user);
        _context.SaveChanges();

        return RedirectToPage("/Account/Users");
    }

    private void LoadCheckpoints()
    {
        Checkpoints = new SelectList(
            _context.Checkpoints
                .OrderBy(c => c.Name)
                .ToList(),
            "Id",
            "Name"
        );
    }
}