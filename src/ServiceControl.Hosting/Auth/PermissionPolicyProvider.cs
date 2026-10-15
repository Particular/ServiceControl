#nullable enable
namespace ServiceControl.Hosting.Auth;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;

/// <summary>
/// A dynamic <see cref="IAuthorizationPolicyProvider"/> that resolves a verb-level authorization policy
/// for each known permission string (e.g. <c>error:messages:retry</c>).
/// <para>
/// The set of valid policy names is known up front (<see cref="Permissions.All"/>), so every policy is
/// <b>built once</b> at construction into a <see cref="FrozenDictionary{TKey, TValue}"/>. The framework
/// calls <see cref="GetPolicyAsync"/> on every request to a protected endpoint, so this makes that call
/// an O(1) lookup with no per-request policy allocation. (Authorization policies and requirements are
/// immutable, so the prebuilt instances are safely shared across all requests.)
/// </para>
/// <para>
/// The policy each permission maps to depends on both authentication settings:
/// <list type="bullet">
/// <item>Authentication disabled: the platform runs unauthenticated, so every permission maps to a
/// shared allow-all policy (no requirement, no handler). Role-based authorization is ignored.</item>
/// <item>Authentication enabled, role-based authorization disabled: every permission maps to a shared
/// policy that only requires an authenticated user.</item>
/// <item>Authentication and role-based authorization enabled: each permission maps to a policy that
/// requires an authenticated user and carries a <see cref="PermissionRequirement"/> (evaluated by
/// <see cref="PermissionVerbHandler"/>).</item>
/// </list>
/// Every controller action carries a permission policy, so the fallback policy never applies to them.
/// These policies must therefore enforce authentication themselves whenever it is enabled.
/// Unknown policy names resolve to <see langword="null"/>; the default and fallback policies are
/// delegated to the configured <see cref="AuthorizationOptions"/>.
/// </para>
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> authorizationOptions, OpenIdConnectSettings oidcSettings)
    : IAuthorizationPolicyProvider
{
    // Carries no requirement, so it succeeds without any IAuthorizationHandler being registered.
    static readonly AuthorizationPolicy AllowAll =
        new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();

    static readonly AuthorizationPolicy AuthenticatedOnly =
        new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    readonly FrozenDictionary<string, AuthorizationPolicy> policies = BuildPolicies(oidcSettings);

    static FrozenDictionary<string, AuthorizationPolicy> BuildPolicies(OpenIdConnectSettings oidcSettings) =>
        Permissions.All.ToFrozenDictionary(
            permission => permission,
            permission => BuildPolicy(permission, oidcSettings),
            StringComparer.Ordinal);

    static AuthorizationPolicy BuildPolicy(string permission, OpenIdConnectSettings oidcSettings)
    {
        if (!oidcSettings.Enabled)
        {
            return AllowAll;
        }

        if (!oidcSettings.RoleBasedAuthorizationEnabled)
        {
            return AuthenticatedOnly;
        }

        return new AuthorizationPolicyBuilder()
            // RequireAuthenticatedUser() must come first so an unauthenticated request fails as
            // FailedAuthentication (→ 401 challenge) rather than FailedRequirements (→ 403
            // forbid). Without it, PermissionVerbHandler is reached for anonymous callers and a
            // missing-roles outcome is classified as a forbidden permission failure.
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }

#pragma warning disable PS0018 // IAuthorizationPolicyProvider declares these without a CancellationToken
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
        Task.FromResult(policies.GetValueOrDefault(policyName));

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        var defaultPolicy = authorizationOptions.Value.DefaultPolicy
            ?? new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        return Task.FromResult(defaultPolicy);
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => Task.FromResult(authorizationOptions.Value.FallbackPolicy);
#pragma warning restore PS0018
}
