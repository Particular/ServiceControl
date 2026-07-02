#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System;
using System.Linq;
using System.Security.Claims;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
class EffectivePermissionsTests
{
    static readonly SettingsRootNamespace TestNamespace = new("ServiceControl");

    static ClaimsPrincipal PrincipalWithRoles(params string[] roles) =>
        new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "test"));

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ROLEBASEDAUTHORIZATIONENABLED", null);
    }

    [Test]
    public void Rbac_enabled_returns_the_union_of_role_permissions()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_AUTHENTICATION_ROLEBASEDAUTHORIZATIONENABLED", "true");
        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: false);

        var result = EffectivePermissions.ForUser(PrincipalWithRoles(RolePermissions.Reader), settings);

        Assert.That(result, Is.EquivalentTo(RolePermissions.Roles[RolePermissions.Reader]));
    }

    [Test]
    public void Rbac_disabled_returns_all_permissions()
    {
        var settings = new OpenIdConnectSettings(TestNamespace, validateConfiguration: false);

        var result = EffectivePermissions.ForUser(PrincipalWithRoles("anything"), settings);

        Assert.That(result, Is.EquivalentTo(Permissions.All));
    }
}
