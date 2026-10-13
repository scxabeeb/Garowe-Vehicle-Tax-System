using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VehicleTax.Web.Services;

namespace VehicleTax.Web.Controllers;

/// <summary>Switches the web UI language and returns to the same page.</summary>
[AllowAnonymous]
public class SetLanguageController : Controller
{
    [HttpGet("/set-language/{code}")]
    public IActionResult Set(string code, [FromQuery] string? returnUrl)
    {
        Response.Cookies.Append(Lang.CookieName, code == Lang.Somali ? Lang.Somali : Lang.English,
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                HttpOnly = true,
            });

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
