#nullable enable
namespace ServiceControl.Hosting.Auth;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ServiceControl.Infrastructure.Auth;

/// <summary>
/// Verb-level authorization handler for <see cref="PermissionRequirement"/>. It resolves the user's
/// roles and checks them against the hardcoded <see cref="RolePermissions"/> policy: the user must hold
/// a role (e.g. <c>reader</c> / <c>writer</c>) that grants the requested permission.
/// <para>
/// Only registered — and only reached — when OIDC is enabled. When it is disabled,
/// <see cref="PermissionPolicyProvider"/> returns an allow-all policy that carries no
/// <see cref="PermissionRequirement"/>, so this handler is not needed.
/// </para>
/// </summary>
public sealed class PermissionVerbHandler : AuthorizationHandler<PermissionRequirement>
{
    public PermissionVerbHandler(string rolesClaimName)
    {
        RoleClaimType = rolesClaimName;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var roles = context.User.FindAll(RoleClaimType).Select(claim => claim.Value);

        if (RolePermissions.IsGranted(roles, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        // Otherwise leave the requirement unmet → the request is denied (403/401).
        return Task.CompletedTask;
    }

    internal string RoleClaimType = "roles";
}