#nullable enable
namespace ServiceControl.Hosting.Auth;

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;

/// <summary>
/// Verb-level authorization handler for <see cref="PermissionRequirement"/>. It resolves the user's
/// roles and checks them against the hardcoded <see cref="RolePermissions"/> policy: the user must hold
/// a role (e.g. <c>reader</c> / <c>writer</c>) that grants the requested permission. Every decision is
/// captured through <see cref="IAuthorizationAuditLog"/> for compliance.
/// <para>
/// Only registered — and only reached — when OIDC is enabled. When it is disabled,
/// <see cref="PermissionPolicyProvider"/> returns an allow-all policy that carries no
/// <see cref="PermissionRequirement"/>, so this handler is not needed.
/// </para>
/// </summary>
public sealed class PermissionVerbHandler(
    IAuthorizationAuditLog auditLog,
    OpenIdConnectSettings oidcSettings)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Unauthenticated requests have no subject and no roles. The framework will challenge with
        // 401 because the policy also includes RequireAuthenticatedUser; skipping here keeps the
        // audit log restricted to identified principals.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var subjectId = context.User.RequireClaim(oidcSettings.SubjectIdClaim, "Authentication.SubjectIdClaim");
        var subjectName = context.User.RequireClaim(oidcSettings.SubjectNameClaim, "Authentication.SubjectNameClaim");
        var roles = context.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        var permission = requirement.Permission;

        if (RolePermissions.IsGranted(roles, permission))
        {
            auditLog.Decision(
                subjectId,
                subjectName,
                permission,
                resource: null,
                allowed: true,
                reason: roles.Length == 0
                    ? $"User holds '{permission}'"
                    : $"User holds '{permission}' via role(s) [{string.Join(", ", roles)}]",
                roles: roles);

            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        auditLog.Decision(
            subjectId,
            subjectName,
            permission,
            resource: null,
            allowed: false,
            reason: roles.Length == 0
                ? $"User has no roles granting '{permission}'"
                : $"None of the user's role(s) [{string.Join(", ", roles)}] grants '{permission}'",
            roles: roles);

        // Leave the requirement unmet → the framework forbids (403).
        return Task.CompletedTask;
    }
}