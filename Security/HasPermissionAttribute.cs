using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VehicleTax.Web.Security;

/// <summary>
/// Server-side permission enforcement for Razor Pages and API controllers.
/// Applied as an authorization filter so restricted pages/endpoints can never be
/// reached by typing a URL or calling an API directly — hiding menu items is
/// cosmetic only, this attribute is the actual gate.
///
/// Usage:  [HasPermission(AppPermissions.Keys.VehiclesView)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;

    public HasPermissionAttribute(string permission) => _permission = permission;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var permissions = context.HttpContext.RequestServices
            .GetRequiredService<Services.IPermissionService>();

        if (!await permissions.HasAsync(user, _permission))
        {
            context.Result = new ForbidResult();
        }
    }
}
