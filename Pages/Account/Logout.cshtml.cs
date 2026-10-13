using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VehicleTax.Web.Models;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Pages.Account;

// Called by logoutUser() in _Layout with fetch (no form token).
[IgnoreAntiforgeryToken]
public class LogoutModel : PageModel
{
    private readonly IActivityLogger _activity;

    public LogoutModel(IActivityLogger activity) => _activity = activity;

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
            await _activity.LogAsync(ActivityActions.Logout, User.Identity.Name, details: "Web");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(".AspNetCore.Identity.Application");

        // Just return OK, JS will handle navigation
        return new OkResult();
    }
}