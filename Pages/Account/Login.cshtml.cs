using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using VehicleTax.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace VehicleTax.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly AppDbContext _context;

    public LoginModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public string Username { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        // Find user by username and password (PLAIN TEXT, matches your DB)
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == Username && u.Password == Password);

        // Invalid credentials
        if (user == null)
        {
            ErrorMessage = "Invalid username or password";
            return Page();
        }

        // 🔒 Block locked users
        if (user.IsLocked)
        {
            ErrorMessage = "Your account is locked. Please contact the administrator.";
            return Page();
        }

        // Build claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        // Add permission claims
        if (!string.IsNullOrWhiteSpace(user.Permissions))
        {
            foreach (var p in user.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim("permission", p.Trim()));
            }
        }

        // Use COOKIE authentication (not Identity)
        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false
            });

        // Redirect after successful login
        return RedirectToPage("/Index");
    }
}
