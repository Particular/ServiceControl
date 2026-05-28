#nullable enable
namespace ServiceControl.Infrastructure.WebApi.Auth;

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth.Rbac;

/// <summary>
/// Registers the S2 policy-attribute authorization services.
/// <para>
/// The <see cref="PermissionPolicyProvider"/> is registered unconditionally so that
/// <c>[Authorize(Policy = "messages:retry")]</c> attributes do not cause "policy not found"
/// errors when OIDC is disabled. When OIDC is disabled, it returns allow-all policies.
/// </para>
/// <para>
/// When OIDC is disabled, an <see cref="AllowAllResourceScopeChecker"/> is registered so that
/// controllers that inject <see cref="IResourceScopeChecker"/> still resolve. The no-op
/// always returns null (allow), preserving the pre-RBAC behaviour.
/// </para>
/// <para>
/// Call this after <c>AddServiceControlAuthentication</c> in the host setup.
/// </para>
/// </summary>
public static class S2AuthorizationExtensions
{
    public static void AddServiceControlS2Authorization(
        this IHostApplicationBuilder hostBuilder,
        OpenIdConnectSettings oidcSettings)
    {
        var services = hostBuilder.Services;

        // Register PermissionPolicyProvider unconditionally so [Authorize(Policy=...)] attributes
        // do not throw "policy not found" errors regardless of OIDC being enabled or disabled.
        // When oidcEnabled=false it returns allow-all policies; when true, real policies.
        services.AddSingleton<IAuthorizationPolicyProvider>(sp =>
            new PermissionPolicyProvider(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>(),
                oidcSettings.Enabled));

        if (!oidcSettings.Enabled)
        {
            // OIDC disabled: register no-op implementations of all S2 services so controllers
            // that inject IResourceScopeChecker or IPermissionEvaluator still resolve.
            // Both are allow-all — the verb gate is bypassed when OIDC is disabled, so none
            // of the enforcement paths are reached; but the no-ops are safe fallbacks that
            // preserve pre-RBAC behaviour (no filtering, no scope restriction).
            services.AddSingleton<IResourceScopeChecker, AllowAllResourceScopeChecker>();
            services.AddSingleton<IPermissionEvaluator, AllowAllPermissionEvaluator>();
            return;
        }

        // OIDC enabled: register the real scope checker and the verb-level handler.
        services.AddSingleton<IResourceScopeChecker, ResourceScopeChecker>();
        services.AddSingleton<IAuthorizationHandler, PermissionVerbHandler>();
    }

    /// <summary>
    /// A no-op <see cref="IResourceScopeChecker"/> that always allows access.
    /// Registered when OIDC is disabled to preserve the pre-RBAC behaviour.
    /// </summary>
    sealed class AllowAllResourceScopeChecker : IResourceScopeChecker
    {
        public Task<IActionResult?> EnforceAsync(
            ClaimsPrincipal user,
            string permission,
            string? queueAddress,
            HttpContext context) =>
            Task.FromResult<IActionResult?>(null);
    }

    /// <summary>
    /// A no-op <see cref="IPermissionEvaluator"/> that always allows access.
    /// Registered when OIDC is disabled to preserve the pre-RBAC behaviour.
    /// <para>
    /// <see cref="ResolveQueueScope"/> returns <see langword="null"/> (unrestricted — no filter),
    /// and <see cref="HasUnrestrictedGrant"/> returns <see langword="true"/>,
    /// so no queue-scope filtering is applied and no fail-closed logic triggers.
    /// </para>
    /// </summary>
    sealed class AllowAllPermissionEvaluator : IPermissionEvaluator
    {
        public bool HasPermission(ClaimsPrincipal user, string permission) => true;
        public bool IsInScope(ClaimsPrincipal user, string permission, string resource) => true;
        public bool HasUnrestrictedGrant(ClaimsPrincipal user, string permission) => true;
        public ResourceScope? ResolveQueueScope(ClaimsPrincipal user, string permission) => null;
        public EffectivePermissions Resolve(ClaimsPrincipal user) => new([]);
    }
}
