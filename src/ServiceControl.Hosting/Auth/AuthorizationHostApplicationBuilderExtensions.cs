namespace ServiceControl.Hosting.Auth;

using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth.Rbac;

/// <summary>
/// Registers the ServiceControl RBAC authorization services.
/// Mirrors <see cref="HostApplicationBuilderExtensions.AddServiceControlAuthentication"/> —
/// early-returns when OIDC is disabled so existing deployments are byte-for-byte unchanged.
/// </summary>
public static class AuthorizationHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the ServiceControl authorization services.
    /// Does nothing when <paramref name="oidcSettings"/>.Enabled is <see langword="false"/>.
    /// </summary>
    public static void AddServiceControlAuthorization(
        this IHostApplicationBuilder hostBuilder,
        OpenIdConnectSettings oidcSettings)
    {
        if (!oidcSettings.Enabled)
        {
            return;
        }

        // Note: the authenticated-user FallbackPolicy (spec §5.5) is intentionally NOT registered
        // here — it is already wired up by AddServiceControlAuthentication via AddAuthorization()
        // in HostApplicationBuilderExtensions. Duplicating it here would be redundant.

        // The policy is loaded once at startup (reloading is a later enhancement).
        // We capture the load time so the descriptor endpoint can expose it as the 'version' field.
        var policyFilePath = ResolveRbacPolicyPath(oidcSettings.RbacPolicyFile);
        var policy = RbacPolicyLoader.LoadFromFile(policyFilePath);

        // Register as a singleton factory so Phase 1 can swap the policy at runtime.
        hostBuilder.Services.AddSingleton<Func<RbacPolicy>>(() => policy);

        hostBuilder.Services.AddSingleton<IPermissionEvaluator>(sp =>
            new PermissionEvaluator(sp.GetRequiredService<Func<RbacPolicy>>()));

        hostBuilder.Services.AddSingleton<IAuthorizationAuditLog, AuthorizationAuditLog>();

        // Ensure the claims transformation runs for every request so realm_access roles
        // are flattened into individual 'role' claims before authorization is evaluated.
        hostBuilder.Services.AddSingleton<
            Microsoft.AspNetCore.Authentication.IClaimsTransformation,
            RealmAccessClaimsTransformation>();
    }

    /// <summary>
    /// Resolves the RBAC policy file path. If the configured path is not absolute,
    /// resolve it relative to the directory containing the host assembly (i.e. the output folder).
    /// </summary>
    static string ResolveRbacPolicyPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, configuredPath);
    }
}
