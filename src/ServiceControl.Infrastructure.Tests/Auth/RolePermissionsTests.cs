#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System.Linq;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;

[TestFixture]
class RolePermissionsTests
{
    [Test]
    public void Every_permission_is_assigned_to_a_role()
    {
        var unassigned = Permissions.All
            .Except(RolePermissions.Roles[RolePermissions.Admin])
            .Order()
            .ToArray();

        Assert.That(unassigned, Is.Empty,
            $"Every permission constant must be assigned to a role group in RolePermissions. Unassigned: [{string.Join(", ", unassigned)}]");
    }

    [Test]
    public void Admin_is_a_proper_superset_of_the_other_roles()
    {
        var reader = RolePermissions.Roles[RolePermissions.Reader];
        var writer = RolePermissions.Roles[RolePermissions.Writer];
        var admin = RolePermissions.Roles[RolePermissions.Admin];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reader.IsProperSubsetOf(admin), Is.True, "reader must be a proper subset of admin");
            Assert.That(writer.IsProperSubsetOf(admin), Is.True, "writer must be a proper subset of admin");
        }
    }

    [Test]
    public void Writer_has_no_access_to_configuration_areas()
    {
        var writer = RolePermissions.Roles[RolePermissions.Writer];

        string[] configurationAreaPermissions =
        [
            Permissions.ErrorLicensingView,
            Permissions.ErrorLicensingManage,
            Permissions.ErrorNotificationsView,
            Permissions.ErrorNotificationsManage,
            Permissions.ErrorNotificationsTest,
            Permissions.ErrorRedirectsView,
            Permissions.ErrorRedirectsManage,
            Permissions.ErrorThroughputView,
            Permissions.ErrorThroughputManage,
        ];

        var granted = configurationAreaPermissions.Where(writer.Contains).ToArray();

        Assert.That(granted, Is.Empty,
            $"Writer must not hold licensing/notifications/redirects/throughput permissions. Granted: [{string.Join(", ", granted)}]");
    }

    [Test]
    public void Role_sets_contain_only_known_permissions()
    {
        foreach (var (role, granted) in RolePermissions.Roles)
        {
            var unknown = granted.Except(Permissions.All).Order().ToArray();

            Assert.That(unknown, Is.Empty,
                $"Role '{role}' grants permissions that are not declared on Permissions: [{string.Join(", ", unknown)}]");
        }
    }
}
