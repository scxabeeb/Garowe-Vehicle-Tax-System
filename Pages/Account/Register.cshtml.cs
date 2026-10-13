using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Account;

[Authorize] // access by permission (Security/PagePermissionFilter: Users.Create)
public class RegisterModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IPermissionService _permissions;

    public RegisterModel(AppDbContext context, IPasswordHasher<User> passwordHasher, IPermissionService permissions)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _permissions = permissions;
    }

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string FullName { get; set; } = "";
    [BindProperty] public string Phone { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string Role { get; set; } = "Collector";
    [BindProperty] public int? CheckpointId { get; set; }

    public UserFormModel Form { get; private set; } = new();

    public async Task OnGetAsync() => await BuildFormAsync(new List<string>());

    public async Task<IActionResult> OnPostAsync()
    {
        var username = (Username ?? "").Trim();
        var roles = await UserFormSupport.LoadRolesAsync(_context, _permissions);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(username)) errors.Add("Username is required.");
        else if (_context.Users.Any(u => u.Username.ToLower() == username.ToLower())) errors.Add("Username already exists.");
        if (string.IsNullOrWhiteSpace(Password) || Password.Trim().Length < UserFormSupport.MinPasswordLength)
            errors.Add("Password must be at least 6 characters.");
        if (!roles.Any(r => r.Name.Equals(Role ?? "", StringComparison.OrdinalIgnoreCase))) errors.Add("Choose a role.");
        if (CheckpointId.HasValue && !_context.Checkpoints.Any(c => c.Id == CheckpointId.Value))
            errors.Add("Selected checkpoint does not exist.");

        if (errors.Any())
        {
            await BuildFormAsync(errors);
            return Page();
        }

        var user = new User
        {
            Username = username,
            FullName = FullName?.Trim() ?? "",
            Phone = Phone?.Trim() ?? "",
            Role = roles.First(r => r.Name.Equals(Role, StringComparison.OrdinalIgnoreCase)).Name,
            Permissions = "",
            CheckpointId = CheckpointId,
        };
        user.Password = _passwordHasher.HashPassword(user, Password.Trim());

        _context.Users.Add(user);
        _context.SaveChanges();

        TempData["Message"] = $"User '{user.Username}' was created.";
        return RedirectToPage("/Account/Users");
    }

    private async Task BuildFormAsync(List<string> errors)
    {
        Form = new UserFormModel
        {
            Username = Username,
            FullName = FullName,
            Phone = Phone,
            Role = Role,
            CheckpointId = CheckpointId,
            Roles = await UserFormSupport.LoadRolesAsync(_context, _permissions),
            Checkpoints = UserFormSupport.LoadCheckpoints(_context, CheckpointId),
            Errors = errors,
        };
    }
}
