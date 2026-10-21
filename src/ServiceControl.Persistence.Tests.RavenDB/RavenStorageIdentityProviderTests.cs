namespace ServiceControl.Persistence.Tests.RavenDB;

using System;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDB;

[TestFixture]
class RavenStorageIdentityProviderTests
{
    [TestCase("http://localhost:33334")]
    [TestCase("http://127.0.0.1:33334")]
    [TestCase("http://[::1]:33334")]
    public void A_loopback_server_is_this_machine(string url) =>
        Assert.That(RavenStorageIdentityProvider.NormalizeServer(url), Is.EqualTo($"{Environment.MachineName}:33334"));

    [Test]
    public void A_remote_server_is_its_host_and_port() =>
        Assert.That(RavenStorageIdentityProvider.NormalizeServer("https://raven.example.com:8080"), Is.EqualTo("raven.example.com:8080"));
}
