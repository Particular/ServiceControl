#nullable enable
namespace ServiceControl.Infrastructure.Auth;

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

/// <summary>
/// The set of permissions a principal effectively holds, computed per request. Mirrors the inputs the
/// enforcement handler uses: when role-based authorization is enabled, the union of the permissions
/// granted by the principal's <see cref="ClaimTypes.Role"/> claims (via <see cref="RolePermissions"/>);
/// when it is disabled the platform runs allow-all, so every known permission is held.
/// </summary>
public static class EffectivePermissions
{
    public static IReadOnlySet<string> ForUser(ClaimsPrincipal user, OpenIdConnectSettings settings)
    {
        if (!settings.RoleBasedAuthorizationEnabled)
        {
            return Permissions.All;
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value);
        return RolePermissions.GetPermissions(roles);
    }
}
