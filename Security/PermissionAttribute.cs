using Microsoft.AspNetCore.Authorization;

namespace VehicleTax.Web.Security;

public class PermissionAttribute : AuthorizeAttribute
{
    public PermissionAttribute(string permission)
    {
        Policy = permission;
    }
}
