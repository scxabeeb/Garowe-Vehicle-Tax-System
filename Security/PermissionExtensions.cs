using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VehicleTax.Web.Security;

public static class PermissionExtensions
{
    public static IActionResult? Require(this PageModel page, string permission)
    {
        if (!page.User.HasClaim("permission", permission))
            return page.RedirectToPage("/Account/AccessDenied");

        return null;
    }
}
