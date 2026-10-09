#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Hosting.Auth;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;

/// <summary>
/// Evaluates every permission policy through the same registrations the instances use, for each
/// combination of <c>Authentication.Enabled</c> and <c>Authentication.RoleBasedAuthorizationEnabled</c>.
/// Every controller action carries a permission policy, so the fallback policy never applies to them:
/// the permission policies alone decide whether an anonymous caller gets in.
/// </summary>
// The settings under test are process-wide environment variables, so these cannot run beside
// each other: one test's setup overwrites what another is about to read.
[TestFixture]
[NonParallelizable]
class PermissionPolicyProviderTests
{
    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", null);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ROLEBASEDAUTHORIZATIONENABLED", null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Anonymous_callers_are_denied_every_permission_when_authentication_is_enabled(bool roleBasedAuthorizationEnabled)
    {
        using var host = BuildHost(authenticationEnabled: true, roleBasedAuthorizationEnabled);

        Assert.That(await AllowedPermissions(host, Anonymous, TestContext.CurrentContext.CancellationToken), Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Anonymous_callers_are_allowed_every_permission_when_authentication_is_disabled(bool roleBasedAuthorizationEnabled)
    {
        using var host = BuildHost(authenticationEnabled: false, roleBasedAuthorizationEnabled);

        Assert.That(await DeniedPermissions(host, Anonymous, TestContext.CurrentContext.CancellationToken), Is.Empty);
    }

    [Test]
    public async Task Authenticated_callers_without_a_role_are_allowed_every_permission_when_role_based_authorization_is_disabled()
    {
        using var host = BuildHost(authenticationEnabled: true, roleBasedAuthorizationEnabled: false);

        Assert.That(await DeniedPermissions(host, Authenticated(), TestContext.CurrentContext.CancellationToken), Is.Empty);
    }

    [Test]
    public async Task Authenticated_callers_without_a_role_are_denied_every_permission_when_role_based_authorization_is_enabled()
    {
        using var host = BuildHost(authenticationEnabled: true, roleBasedAuthorizationEnabled: true);

        Assert.That(await AllowedPermissions(host, Authenticated(), TestContext.CurrentContext.CancellationToken), Is.Empty);
    }

    [Test]
    public async Task Admins_are_allowed_every_permission_when_role_based_authorization_is_enabled()
    {
        using var host = BuildHost(authenticationEnabled: true, roleBasedAuthorizationEnabled: true);

        Assert.That(await DeniedPermissions(host, Authenticated(RolePermissions.Admin), TestContext.CurrentContext.CancellationToken), Is.Empty);
    }

    // The audit log needs the subject ID and name to identify the caller. A token without them must
    // get 403, not an unhandled exception that ASP.NET Core turns into 500.
    [TestCase("sub")]
    [TestCase("preferred_username")]
    public async Task Admins_without_a_subject_claim_are_denied_every_permission(string missingClaim)
    {
        using var host = BuildHost(authenticationEnabled: true, roleBasedAuthorizationEnabled: true);

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            Authenticated(RolePermissions.Admin).Claims.Where(claim => claim.Type != missingClaim),
            authenticationType: "test"));

        Assert.That(await AllowedPermissions(host, user, TestContext.CurrentContext.CancellationToken), Is.Empty);
    }

    static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    static ClaimsPrincipal Authenticated(params string[] roles) =>
        new(new ClaimsIdentity(
            [
                new Claim("sub", "alice-sub"),
                new Claim("preferred_username", "Alice"),
                .. roles.Select(role => new Claim(ClaimTypes.Role, role))
            ],
            authenticationType: "test"));

    static IHost BuildHost(bool authenticationEnabled, bool roleBasedAuthorizationEnabled)
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ENABLED", authenticationEnabled ? bool.TrueString : bool.FalseString);
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ROLEBASEDAUTHORIZATIONENABLED", roleBasedAuthorizationEnabled ? bool.TrueString : bool.FalseString);

        var settings = new OpenIdConnectSettings(new SettingsRootNamespace("ServiceControl"), validateConfiguration: false, requireServicePulseSettings: false);

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddLogging();
        builder.AddServiceControlAuthentication(settings);
        builder.AddServiceControlAuthorization(settings);
        return builder.Build();
    }

    static Task<string[]> AllowedPermissions(IHost host, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        PermissionsWhere(host, user, succeeded: true, cancellationToken);

    static Task<string[]> DeniedPermissions(IHost host, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        PermissionsWhere(host, user, succeeded: false, cancellationToken);

    static async Task<string[]> PermissionsWhere(IHost host, ClaimsPrincipal user, bool succeeded, CancellationToken cancellationToken)
    {
        var authorizationService = host.Services.GetRequiredService<IAuthorizationService>();
        var matches = new List<string>();

        foreach (var permission in Permissions.All.Order())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await authorizationService.AuthorizeAsync(user, resource: null, permission);
            if (result.Succeeded == succeeded)
            {
                matches.Add(permission);
            }
        }

        return [.. matches];
    }
}
