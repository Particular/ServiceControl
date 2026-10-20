namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.RavenDB;

    [TestFixture]
    class RavenStorageIdentityProviderTests
    {
        [TestCase("http://localhost:44445")]
        [TestCase("http://127.0.0.1:44445")]
        [TestCase("http://[::1]:44445")]
        public void A_loopback_server_is_this_machine(string url) =>
            Assert.That(RavenStorageIdentityProvider.NormalizeServer(url), Is.EqualTo($"{Environment.MachineName}:44445"));

        [Test]
        public void A_remote_server_is_its_host_and_port() =>
            Assert.That(RavenStorageIdentityProvider.NormalizeServer("https://raven.example.com:8080"), Is.EqualTo("raven.example.com:8080"));
    }
}
