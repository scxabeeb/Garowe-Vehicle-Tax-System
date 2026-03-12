using System.Security.Claims;

namespace VehicleTax.Web.Security
{
    public static class SecurityPolicy
    {
        public static bool CanAccess(SecurityOperation operation, ClaimsPrincipal user)
        {
            return operation switch
            {
                SecurityOperation.AccountStatus =>
                    user.IsInRole("Admin") || user.IsInRole("SuperAdmin"),

                SecurityOperation.PasswordReset =>
                    user.IsInRole("SuperAdmin"),

                _ => false
            };
        }
    }
}
