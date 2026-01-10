using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace VehicleTax.Web.Security;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var claims = context.User.FindAll("permission")
            .Select(c => c.Value)
            .ToList();

        if (claims.Contains(requirement.Permission) || context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
