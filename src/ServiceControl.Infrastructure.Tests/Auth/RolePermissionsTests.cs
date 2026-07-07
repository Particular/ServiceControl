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
}
