#nullable enable
namespace ServiceControl.Infrastructure.WebApi.Auth;

using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ServiceControl.Infrastructure.Auth.Rbac;

/// <summary>
/// S2-specific contract for inline resource-scope checking.
/// <para>
/// In S2 the controller calls <see cref="EnforceAsync"/> after loading the resource,
/// passing the resolved queue address. The checker handles deny (writes the structured-403
/// body and returns a non-null result to short-circuit) and allow (logs and returns null).
/// </para>
/// </summary>
public interface IResourceScopeChecker
{
    /// <summary>
    /// Checks whether <paramref name="user"/> is permitted to perform <paramref name="permission"/>
    /// on the resource identified by <paramref name="queueAddress"/>.
    /// </summary>
    /// <param name="user">The authenticated user principal.</param>
    /// <param name="permission">The permission being enforced (e.g. <c>messages:retry</c>).</param>
    /// <param name="queueAddress">
    /// The queue address of the resource. When null or empty the check fails closed (deny).
    /// </param>
    /// <param name="context">The HTTP context, used to write the structured 403 body on deny.</param>
    /// <returns>
    /// <see langword="null"/> when access is allowed; a non-null <see cref="IActionResult"/>
    /// when access is denied (the 403 body has already been written to <paramref name="context"/>).
    /// </returns>
    Task<IActionResult?> EnforceAsync(
        ClaimsPrincipal user,
        string permission,
        string? queueAddress,
        HttpContext context);
}

/// <summary>
/// Default implementation of <see cref="IResourceScopeChecker"/>.
/// </summary>
public sealed class ResourceScopeChecker(
    IPermissionEvaluator permissionEvaluator,
    IAuthorizationAuditLog auditLog) : IResourceScopeChecker
{
    public async Task<IActionResult?> EnforceAsync(
        ClaimsPrincipal user,
        string permission,
        string? queueAddress,
        HttpContext context)
    {
        var subject = AuthorizationHelpers.GetSubject(user);

        // Fail closed: a resource with no resolvable queue address cannot be scope-checked.
        if (string.IsNullOrEmpty(queueAddress))
        {
            auditLog.Decision(
                subject,
                permission,
                resource: null,
                allowed: false,
                reason: $"Resource-scope check: resource has no resolvable queue address — denying '{permission}' fail-closed");

            await AuthorizationHelpers.WriteScopeDenied403(context.Response, permission, queueAddress: null);
            return new EmptyResult();
        }

        // Resource-scope check: is this resource's queue address in scope for the user?
        if (!permissionEvaluator.IsInScope(user, permission, queueAddress))
        {
            auditLog.Decision(
                subject,
                permission,
                resource: queueAddress,
                allowed: false,
                reason: $"Resource-scope check: queue '{queueAddress}' is out of scope for permission '{permission}'");

            await AuthorizationHelpers.WriteScopeDenied403(context.Response, permission, queueAddress);
            return new EmptyResult();
        }

        auditLog.Decision(
            subject,
            permission,
            resource: queueAddress,
            allowed: true,
            reason: $"Resource-scope check: user holds '{permission}' and queue '{queueAddress}' is in scope");

        return null; // Access allowed — controller proceeds.
    }
}
