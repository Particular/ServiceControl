namespace ServiceControl.Infrastructure.Tests.Auth.Rbac;

using System.Collections.Generic;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth.Rbac;

[TestFixture]
public class RbacPolicyTests
{
    [Test]
    public void Role_exposes_its_permission_grants()
    {
        var role = new RbacRole("sc-operator",
            Bindings: ["role:sc-operator"],
            Permissions: [new PermissionGrant("messages:retry", Scope: null)]);
        var policy = new RbacPolicy(SchemaVersion: 1, Roles: new Dictionary<string, RbacRole> { ["sc-operator"] = role });

        Assert.That(policy.Roles["sc-operator"].Permissions[0].Permission, Is.EqualTo("messages:retry"));
    }
}
