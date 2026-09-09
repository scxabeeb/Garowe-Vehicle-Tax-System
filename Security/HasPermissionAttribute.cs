using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VehicleTax.Web.Security;

/// <summary>
/// Server-side permission enforcement for Razor Pages and API controllers.
/// Restricted pages/endpoints can never be reached by typing a URL or calling an
/// API directly — hiding menu items is cosmetic only, this attribute is the gate.
///
/// Usage:  [HasPermission(AppPermissions.Keys.VehiclesView)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter, IAuthorizeData
{
    private readonly string _permission;

    public HasPermissionAttribute(string permission)
    {
        _permission = permission;
        Policy = $"perm:{permission}";
    }

    // IAuthorizeData implementation — lets Razor Pages / MVC treat this as an
    // authorization policy so the gate runs even when the filter itself isn't invoked.
    public string? Policy { get; set; }
    public string? Roles { get; set; }
    public string? AuthenticationSchemes { get; set; }

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
