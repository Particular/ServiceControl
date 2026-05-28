#nullable enable
namespace ServiceControl.Infrastructure.WebApi.Auth;

using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Infrastructure;

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
            // OIDC disabled: register a no-op scope checker so controllers can inject
            // IResourceScopeChecker unconditionally. The verb gate is allow-all when OIDC
            // is disabled, so EnforceAsync would not be reached; but the no-op is a
            // safe fallback.  IPermissionEvaluator / IAuthorizationAuditLog are not
            // registered when OIDC is disabled, so ResourceScopeChecker cannot be used.
            services.AddSingleton<IResourceScopeChecker, AllowAllResourceScopeChecker>();
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
}
