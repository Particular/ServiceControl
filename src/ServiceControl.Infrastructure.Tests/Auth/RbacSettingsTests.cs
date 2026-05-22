namespace ServiceControl.Infrastructure.Tests.Auth;

using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;

[TestFixture]
public class RbacSettingsTests
{
    [Test]
    public void RbacPolicyFile_defaults_to_rbac_yaml()
    {
        var settings = new OpenIdConnectSettings(new SettingsRootNamespace("ServiceControl"),
            validateConfiguration: false);
        Assert.That(settings.RbacPolicyFile, Is.EqualTo("rbac.yaml"));
    }
}
