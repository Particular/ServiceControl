#nullable enable
namespace ServiceControl.Infrastructure.WebApi.Auth;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ServiceControl.Infrastructure.Auth.Rbac;

/// <summary>
/// A dynamic <see cref="IAuthorizationPolicyProvider"/> that generates verb-level
/// authorization policies from known permission strings (e.g. <c>messages:retry</c>).
/// <para>
/// When the MVC pipeline evaluates <c>[Authorize(Policy = "messages:retry")]</c>, this provider
/// generates a policy containing a <see cref="PermissionRequirement"/> for that permission.
/// <see cref="PermissionVerbHandler"/> evaluates the requirement against the current user.
/// </para>
/// <para>
/// When OIDC is disabled, this provider returns a permissive allow-all policy for any known
/// permission name, preserving the pre-RBAC behaviour (everything allowed).
/// </para>
/// <para>
/// Policy names that are not known permission strings (e.g. names registered via
/// <see cref="AuthorizationOptions"/>) return <see langword="null"/> so the framework
/// falls back to its default policy resolution.
/// </para>
/// </summary>
public sealed class PermissionPolicyProvider(
    IOptions<AuthorizationOptions> authorizationOptions,
    bool oidcEnabled)
    : IAuthorizationPolicyProvider
{
    static readonly AuthorizationPolicy AllowAllPolicy =
        new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();

    /// <summary>
    /// Returns <see langword="true"/> only when <paramref name="policyName"/> is a known permission.
    /// Unknown <c>resource:action</c> strings that happen to contain a colon are not treated as permissions.
    /// </summary>
    static bool IsKnownPermission(string policyName) =>
        Permissions.All.Contains(policyName);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!IsKnownPermission(policyName))
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        if (!oidcEnabled)
        {
            // OIDC disabled → return a permissive allow-all policy so [Authorize(Policy=...)]
            // attributes do not block requests when the authorization middleware is present.
            return Task.FromResult<AuthorizationPolicy?>(AllowAllPolicy);
        }

        // OIDC enabled → build a real policy requiring the named permission.
        // PermissionVerbHandler evaluates HasPermission() before the resource is loaded.
        var policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        var defaultPolicy = authorizationOptions.Value.DefaultPolicy
            ?? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        return Task.FromResult(defaultPolicy);
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        var fallbackPolicy = authorizationOptions.Value.FallbackPolicy;
        return Task.FromResult(fallbackPolicy);
    }
}
