using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
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

    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == Username && u.Password == Password);

        if (user == null)
        {
            ErrorMessage = "Invalid login";
            return Page();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        if (!string.IsNullOrEmpty(user.Permissions))
        {
            foreach (var p in user.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries))
                claims.Add(new Claim("permission", p.Trim()));
        }

        var identity = new ClaimsIdentity(
            claims,
            IdentityConstants.ApplicationScheme
        );

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false
            });

        return RedirectToPage("/Index");
    }
}
