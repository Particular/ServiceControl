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
    // The per-IdP variability of the source claim is absorbed by RolesClaimsTransformation, which
    // reads from the path configured in Authentication.RolesClaim and emits canonical "roles" claims.
    const string RoleClaimType = "roles";

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

        var subjectId = RequireClaim(context.User, oidcSettings.SubjectIdClaim, "Authentication.SubjectIdClaim");
        var subjectName = RequireClaim(context.User, oidcSettings.SubjectNameClaim, "Authentication.SubjectNameClaim");
        var roles = context.User.FindAll(RoleClaimType).Select(claim => claim.Value).ToArray();
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
                    : $"User holds '{permission}' via role(s) [{string.Join(", ", roles)}]");

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
                : $"None of the user's role(s) [{string.Join(", ", roles)}] grants '{permission}'");

        // Leave the requirement unmet → the framework forbids (403).
        return Task.CompletedTask;
    }

    static string RequireClaim(ClaimsPrincipal user, string claimType, string settingName)
    {
        var value = user.FindFirst(claimType)?.Value;
        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException(
                $"Authenticated principal is missing the required '{claimType}' claim configured by {settingName}. " +
                "Configure the identity provider to emit this claim, or point the setting at the claim the IdP actually emits.");
        }
        return value;
    }
}
