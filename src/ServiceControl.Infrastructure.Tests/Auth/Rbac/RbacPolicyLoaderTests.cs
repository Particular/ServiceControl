namespace ServiceControl.Infrastructure.Tests.Auth.Rbac;

using NUnit.Framework;
using ServiceControl.Infrastructure.Auth.Rbac;

[TestFixture]
public class RbacPolicyLoaderTests
{
    [Test]
    public void Parses_roles_permissions_and_scope()
    {
        const string yaml = """
            schemaVersion: 1
            roles:
              sc-operator:
                bindings: [ "role:sc-operator" ]
                permissions:
                  - "messages:view"
                  - permission: "messages:retry"
                    scope: { allow: ["acme.sales.*"], deny: ["acme.sales.secret.*"] }
            """;
        var policy = RbacPolicyLoader.Parse(yaml);

        var ops = policy.Roles["sc-operator"];
        Assert.That(ops.Bindings, Does.Contain("role:sc-operator"));
        Assert.That(ops.Permissions[0].Permission, Is.EqualTo("messages:view"));
        Assert.That(ops.Permissions[0].Scope, Is.Null);
        Assert.That(ops.Permissions[1].Scope!.Allow, Does.Contain("acme.sales.*"));
    }

    [Test]
    public void Invalid_yaml_throws_with_a_clear_message()
        => Assert.That(() => RbacPolicyLoader.Parse("not: : valid"),
            Throws.Exception.With.Message.Contains("rbac"));
}
