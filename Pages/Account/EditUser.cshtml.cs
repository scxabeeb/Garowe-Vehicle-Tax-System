using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Data;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Account;

[Authorize] // access by permission (Security/PagePermissionFilter: Users.Edit)
public class EditUserModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IPermissionService _permissions;

    public EditUserModel(AppDbContext context, IPasswordHasher<User> passwordHasher, IPermissionService permissions)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _permissions = permissions;
    }

    [BindProperty] public int Id { get; set; }
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string FullName { get; set; } = "";
    [BindProperty] public string Phone { get; set; } = "";
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string Role { get; set; } = "";
    [BindProperty] public int? CheckpointId { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;

    public UserFormModel Form { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = _context.Users.FirstOrDefault(u => u.Id == id);
        if (user == null) return RedirectToPage("/Account/Users");

        Id = user.Id;
        Username = user.Username;
        FullName = user.FullName;
        Phone = user.Phone;
        Role = user.Role;
        CheckpointId = user.CheckpointId;
        IsActive = !user.IsLocked;
        await BuildFormAsync(user, new List<string>());
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var dbUser = _context.Users.FirstOrDefault(u => u.Id == id);
        if (dbUser == null) return RedirectToPage("/Account/Users");

        var username = (Username ?? "").Trim();
        var roles = await UserFormSupport.LoadRolesAsync(_context, _permissions, keep: dbUser.Role);
        var isMe = string.Equals(User.Identity?.Name, dbUser.Username, StringComparison.OrdinalIgnoreCase);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(username)) errors.Add("Username is required.");
        else if (_context.Users.Any(u => u.Id != id && u.Username.ToLower() == username.ToLower())) errors.Add("Username already exists.");
        if (!string.IsNullOrWhiteSpace(Password) && Password.Trim().Length < UserFormSupport.MinPasswordLength)
            errors.Add("Password must be at least 6 characters.");
        if (!roles.Any(r => r.Name.Equals(Role ?? "", StringComparison.OrdinalIgnoreCase))) errors.Add("Choose a role.");
        if (isMe && !string.Equals(Role, dbUser.Role, StringComparison.OrdinalIgnoreCase))
            errors.Add("You cannot change your own role.");
        if (isMe && !string.Equals(username, dbUser.Username, StringComparison.Ordinal))
            errors.Add("You cannot change your own username while signed in.");
        if (isMe && !IsActive)
            errors.Add("You cannot set your own account to inactive.");
        if (CheckpointId.HasValue && !_context.Checkpoints.Any(c => c.Id == CheckpointId.Value))
            errors.Add("Selected checkpoint does not exist.");

        if (errors.Any())
        {
            Id = id;
            await BuildFormAsync(dbUser, errors);
            return Page();
        }

        dbUser.Username = username;
        dbUser.FullName = FullName?.Trim() ?? "";
        dbUser.Phone = Phone?.Trim() ?? "";
        dbUser.Role = roles.First(r => r.Name.Equals(Role, StringComparison.OrdinalIgnoreCase)).Name;
        dbUser.CheckpointId = CheckpointId;
        dbUser.IsLocked = !IsActive;
        if (!string.IsNullOrWhiteSpace(Password))
            dbUser.Password = _passwordHasher.HashPassword(dbUser, Password.Trim());

        _context.SaveChanges();
        TempData["Message"] = $"User '{dbUser.Username}' was updated.";
        return RedirectToPage("/Account/Users");
    }

    private async Task BuildFormAsync(User user, List<string> errors)
    {
        Form = new UserFormModel
        {
            IsEdit = true,
            Id = Id,
            Username = Username,
            FullName = FullName,
            Phone = Phone,
            Role = Role,
            CheckpointId = CheckpointId,
            IsLocked = !IsActive,
            IsMe = string.Equals(User.Identity?.Name, user.Username, StringComparison.OrdinalIgnoreCase),
            Roles = await UserFormSupport.LoadRolesAsync(_context, _permissions, keep: user.Role),
            Checkpoints = UserFormSupport.LoadCheckpoints(_context, CheckpointId),
            Errors = errors,
        };
    }
}
