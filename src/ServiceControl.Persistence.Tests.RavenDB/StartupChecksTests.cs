namespace ServiceControl.Persistence.Tests.RavenDB;

using System;
using NUnit.Framework;
using ServiceControl.RavenDB;

[TestFixture]
class StartupChecksTests
{
    static readonly Version Client727 = new(7, 2, 7);

    [TestCase("6.2.0")]
    [TestCase("6.2.20")]
    [TestCase("7.1.9")]
    public void Should_reject_servers_with_a_lower_major_minor_version_than_the_client(string serverVersion)
    {
        var isValid = StartupChecks.TryValidateServerVersion(serverVersion, Client727, out var error);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Does.Contain("expects RavenDB Server version 7.2 or higher")
                .And.Contain($"the server is using {serverVersion}")
                .And.Contain("servicecontrol-ravendb container"));
        }
    }

    [Test]
    public void Should_reject_servers_with_a_lower_major_minor_version_than_a_newer_client()
    {
        var isValid = StartupChecks.TryValidateServerVersion("7.2.9", new Version(7, 3, 0), out var error);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Does.Contain("expects RavenDB Server version 7.3 or higher").And.Contain("the server is using 7.2.9"));
        }
    }

    [TestCase("7.2.0", "7.2.0")]
    [TestCase("7.2.4", "7.2.4")]
    [TestCase("7.2.4-nightly-20260101-0100", "7.2.4")]
    public void Should_reject_servers_with_the_known_attachments_index_corruption(string serverVersion, string reportedVersion)
    {
        var isValid = StartupChecks.TryValidateServerVersion(serverVersion, Client727, out var error);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Does.Contain($"RavenDB Server {reportedVersion} can corrupt the attachments index")
                .And.Contain("to 7.2.5 or later")
                .And.Contain("servicecontrol-ravendb container"));
        }
    }

    [TestCase("7.2.5")]
    [TestCase("7.2.6")]
    [TestCase("7.2.7")]
    [TestCase("7.2.10")]
    [TestCase("7.2.7+441f983")]
    [TestCase("7.2.8-nightly-20260101-0100")]
    [TestCase("7.3.0")]
    [TestCase("8.0.0")]
    public void Should_accept_compatible_servers_without_the_known_corruption(string serverVersion)
    {
        var isValid = StartupChecks.TryValidateServerVersion(serverVersion, Client727, out var error);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True);
            Assert.That(error, Is.Null);
        }
    }

    [TestCase("7.2.5")]
    [TestCase("7.2.7")]
    public void Should_not_raise_the_floor_with_the_client_patch_version(string serverVersion)
    {
        var isValid = StartupChecks.TryValidateServerVersion(serverVersion, new Version(7, 2, 10), out _);

        Assert.That(isValid, Is.True);
    }

    [TestCase("")]
    [TestCase("unknown")]
    public void Should_reject_unreadable_server_versions(string serverVersion)
    {
        var isValid = StartupChecks.TryValidateServerVersion(serverVersion, Client727, out var error);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(error, Does.Contain($"could not read the server version '{serverVersion}'"));
        }
    }
}
