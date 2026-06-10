#nullable enable
namespace ServiceControl.Hosting.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;

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
        var services = hostBuilder.Services;

        // Ensure the authorization core services and options are present (idempotent).
        services.AddAuthorization();

        // The policy provider is registered UNCONDITIONALLY: every instance hosts controllers with
        // [Authorize(Policy = Permissions.X)] attributes, and without a provider that knows those
        // policy names ASP.NET throws "AuthorizationPolicy named '...' was not found" → 500 on every
        // request to an annotated endpoint. When RBAC is disabled the provider returns allow-all
        // policies (no requirement), so anonymous-to-the-policy calls pass through and the verb
        // handler is unnecessary.
        services.AddSingleton<IAuthorizationPolicyProvider>(sp =>
            new PermissionPolicyProvider(sp.GetRequiredService<IOptions<AuthorizationOptions>>(), oidcSettings));

        // The provider only emits a PermissionRequirement when RBAC is enabled, so the handler is the
        // only thing that evaluates one. It is registered alongside the provider (cheap singleton, never
        // invoked when no requirement is produced). The handler emits an audit-log entry for every
        // decision through IAuthorizationAuditLog so the platform can show, after the fact, who attempted
        // what and how the system responded. The subject-id and subject-name claim names flow through
        // from configuration so the handler can read them off the principal.
        services.AddSingleton<IAuthorizationAuditLog, AuthorizationAuditLog>();
        services.AddSingleton<IAuthorizationHandler>(sp =>
            new PermissionVerbHandler(
                sp.GetRequiredService<IAuthorizationAuditLog>(),
                oidcSettings.SubjectIdClaim,
                oidcSettings.SubjectNameClaim));
    }
}
