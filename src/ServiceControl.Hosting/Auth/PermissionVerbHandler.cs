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
    // TODO: The claim that carries a user's roles is identity-provider specific and must become
    // configurable (per-IdP) rather than hardcoded. Roles are expected as a flat, multivalued claim;
    // the token handler splits a top-level JSON array into individual claims, so no parsing is needed.
    // For Keycloak, add a "User Realm Role" protocol mapper with Multivalued = ON and Token Claim Name
    // = "roles" (a dotted name like "realm_access.roles" would nest it into an object instead).
    const string RoleClaimType = "roles";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var roles = context.User.FindAll(RoleClaimType).Select(claim => claim.Value);


        // TODO: Although plural, likely roles will only contain a single value unless we want to define a role for each instance but likely customers don't care about instances
        if (RolePermissions.IsGranted(roles, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        // Otherwise leave the requirement unmet → the request is denied (403/401).
        return Task.CompletedTask;
    }
}
