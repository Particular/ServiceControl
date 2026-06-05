#nullable enable
namespace ServiceControl.Hosting.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServiceControl.Infrastructure;

/// <summary>
/// Registers the permission-based policy authorization services: a dynamic
/// <see cref="PermissionPolicyProvider"/> that resolves <c>[Authorize(Policy = "&lt;permission&gt;")]</c>
/// attributes, and — when OIDC is enabled — the <see cref="PermissionVerbHandler"/> that evaluates them
/// against the user's roles.
/// <para>
/// The provider is registered unconditionally so the policy attributes resolve in every configuration
/// (without it, annotated endpoints fail with "AuthorizationPolicy not found"). When OIDC is disabled the
/// provider returns allow-all policies that carry no requirement, so the verb handler is not registered.
/// Wire this into every instance that hosts annotated controllers (Error, Audit, Monitoring).
/// </para>
/// </summary>
public static class PermissionAuthorizationExtensions
{
    public static void AddServiceControlAuthorization(this IHostApplicationBuilder hostBuilder, OpenIdConnectSettings oidcSettings)
    {
        if (!oidcSettings.RoleBasedAuthorizationEnabled)
        {
            return;
        }

        var services = hostBuilder.Services;

        // Ensure the authorization core services and options are present (idempotent).
        services.AddAuthorization();

        // Resolve permission policy names dynamically. Registered last so it supersedes the default
        // policy provider registered by AddAuthorization(). When OIDC is disabled it returns allow-all
        // policies (no requirement); when enabled it emits a PermissionRequirement for the verb handler.
        services.AddSingleton<IAuthorizationPolicyProvider>(sp =>
            new PermissionPolicyProvider(sp.GetRequiredService<IOptions<AuthorizationOptions>>(), oidcSettings.Enabled));

        services.AddSingleton<IAuthorizationHandler>(service => new PermissionVerbHandler(oidcSettings.RolesClaim));
    }
}
