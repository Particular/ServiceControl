#nullable enable
namespace ServiceControl.Hosting.Auth;

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;

/// <summary>
/// Verb-level authorization handler for <see cref="PermissionRequirement"/>. It resolves the user's
/// roles and checks them against the hardcoded <see cref="RolePermissions"/> policy: the user must hold
/// a role (e.g. <c>reader</c> / <c>writer</c>) that grants the requested permission. Every decision is
/// captured through <see cref="IAuthorizationAuditLog"/> for compliance.
/// <para>
/// Only reached when authentication and role-based authorization are both enabled. Otherwise
/// <see cref="PermissionPolicyProvider"/> returns policies that carry no
/// <see cref="PermissionRequirement"/>, so this handler is not needed.
/// </para>
/// </summary>
public sealed class PermissionVerbHandler(
    IAuthorizationAuditLog auditLog,
    OpenIdConnectSettings oidcSettings,
    ILogger<PermissionVerbHandler> logger)
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

        var subjectId = context.User.FindFirst(oidcSettings.SubjectIdClaim)?.Value;
        var subjectName = context.User.FindFirst(oidcSettings.SubjectNameClaim)?.Value;

        // The audit log needs both values to identify the caller. Without them the request is
        // forbidden (403), not an unhandled exception (500).
        if (string.IsNullOrEmpty(subjectId) || string.IsNullOrEmpty(subjectName))
        {
            var (claimType, settingName) = string.IsNullOrEmpty(subjectId)
                ? (oidcSettings.SubjectIdClaim, "Authentication.SubjectIdClaim")
                : (oidcSettings.SubjectNameClaim, "Authentication.SubjectNameClaim");

            logger.LogWarning(
                "Access denied: the token has no '{ClaimType}' claim, which is configured by {SettingName}. Configure the identity provider to emit this claim, or change the setting to a claim that the identity provider emits",
                claimType, settingName);

            context.Fail(new AuthorizationFailureReason(this, $"The token has no '{claimType}' claim, which is configured by {settingName}"));
            return Task.CompletedTask;
        }

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
                    : $"User holds '{permission}' via role(s) [{string.Join(", ", roles)}]", roles: roles);

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
                : $"None of the user's role(s) [{string.Join(", ", roles)}] grants '{permission}'", roles: roles);

        // Leave the requirement unmet → the framework forbids (403).
        return Task.CompletedTask;
    }
}