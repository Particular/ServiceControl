#nullable enable
namespace ServiceControl.Infrastructure.WebApi.Auth;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ServiceControl.Infrastructure.Auth.Rbac;

/// <summary>
/// S2 verb-level authorization handler for <see cref="PermissionRequirement"/>.
/// <para>
/// This handler fires when ASP.NET Core evaluates an <c>[Authorize(Policy = "permission")]</c>
/// attribute — i.e., before the controller action runs and before any resource is loaded.
/// It answers the coarse question: "does this user hold the permission at all?"
/// </para>
/// <para>
/// In S2, the resource-scope check is performed inline in the controller via
/// <see cref="IResourceScopeChecker"/>, not via a typed handler. There are therefore no
/// domain-object resources passed through <see cref="IAuthorizationService"/>; the verb handler
/// runs for every request and is the only <see cref="IAuthorizationHandler"/> in the S2 pipeline.
/// </para>
/// </summary>
public sealed class PermissionVerbHandler(
    IPermissionEvaluator permissionEvaluator,
    IAuthorizationAuditLog auditLog)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // S2 uses no explicit resource-scope calls via IAuthorizationService, but guard anyway
        // so the handler is harmless if code ever calls AuthorizeAsync with a resource object.
        if (context.Resource is not null and not Microsoft.AspNetCore.Http.HttpContext)
        {
            return Task.CompletedTask;
        }

        var subject = AuthorizationHelpers.GetSubject(context.User);
        var permission = requirement.Permission;

        if (permissionEvaluator.HasPermission(context.User, permission))
        {
            auditLog.Decision(
                subject,
                permission,
                resource: null,
                allowed: true,
                reason: $"Verb-level check: user holds '{permission}'");

            context.Succeed(requirement);
        }
        else
        {
            auditLog.Decision(
                subject,
                permission,
                resource: null,
                allowed: false,
                reason: $"Verb-level check: user does not hold '{permission}'");

            context.Fail(new AuthorizationFailureReason(
                this,
                $"User '{subject}' does not hold permission '{permission}'"));
        }

        return Task.CompletedTask;
    }
}
