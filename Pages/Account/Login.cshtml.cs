using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using VehicleTax.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using VehicleTax.Web.Models;
using MySqlConnector;

namespace VehicleTax.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public LoginModel(AppDbContext context, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [BindProperty]
    public string Username { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        Username = (Username ?? string.Empty).Trim();
        Password = Password ?? string.Empty;

        User? user;
        try
        {
            // Find user by username, then verify password hash.
            // Legacy plain-text passwords are migrated on successful login.
            user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == Username);
        }
        catch (MySqlException)
        {
            ErrorMessage = "Login service is temporarily unavailable. Please try again.";
            return Page();
        }
        catch (TimeoutException)
        {
            ErrorMessage = "Login request timed out. Please try again.";
            return Page();
        }

        // Invalid credentials
        if (user == null || string.IsNullOrWhiteSpace(user.Password))
        {
            ErrorMessage = "Invalid username or password";
            return Page();
        }

        PasswordVerificationResult verifyResult;
        try
        {
            verifyResult = _passwordHasher.VerifyHashedPassword(user, user.Password, Password);
        }
        catch (FormatException)
        {
            // Stored value is not a valid Identity hash (legacy/plain text or corrupted).
            verifyResult = PasswordVerificationResult.Failed;
        }

        if (verifyResult == PasswordVerificationResult.Failed)
        {
            // Backward compatibility for legacy plain-text passwords.
            if (user.Password != Password)
            {
                ErrorMessage = "Invalid username or password";
                return Page();
            }

            user.Password = _passwordHasher.HashPassword(user, Password);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (MySqlException)
            {
                ErrorMessage = "Login service is temporarily unavailable. Please try again.";
                return Page();
            }
            catch (TimeoutException)
            {
                ErrorMessage = "Login request timed out. Please try again.";
                return Page();
            }
        }
        else if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.Password = _passwordHasher.HashPassword(user, Password);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (MySqlException)
            {
                ErrorMessage = "Login service is temporarily unavailable. Please try again.";
                return Page();
            }
            catch (TimeoutException)
            {
                ErrorMessage = "Login request timed out. Please try again.";
                return Page();
            }
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
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
    }
}
