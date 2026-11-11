namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using System.IO;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    class RavenDBServerPackageTests
    {
        [TestCase("RavenDB7", ExpectedResult = "RavenDB7Server.zip")]
        [TestCase("RavenDB", ExpectedResult = "RavenDBServer.zip")]
        [TestCase("SQLServer", ExpectedResult = "RavenDBServer.zip")]
        [TestCase("PostgreSQL", ExpectedResult = "RavenDBServer.zip")]
        [TestCase("RavenDB35", ExpectedResult = "RavenDBServer.zip")]
        [TestCase("SomeFuturePersister", ExpectedResult = "RavenDBServer.zip")]
        public string Archive_that_matches_the_persister(string persister) =>
            RavenDBServerPackage.ArchiveResourceName(new PersistenceManifest { Name = persister });

        [TestCase("RavenDB7", "RavenDB7")]
        [TestCase("RavenDB", "RavenDB")]
        [TestCase("SQLServer", "RavenDB")]
        [TestCase("PostgreSQL", "RavenDB")]
        public void Server_folder_that_matches_the_persister(string persister, string persisterFolder) =>
            Assert.That(RavenDBServerPackage.ServerDirectory("install", new PersistenceManifest { Name = persister }), Is.EqualTo(Path.Combine("install", "Persisters", persisterFolder, "RavenDBServer")));

        [Test]
        public void A_persister_that_is_not_known_gets_the_RavenDB_6_2_server() =>
            Assert.That(RavenDBServerPackage.ArchiveResourceName(null), Is.EqualTo("RavenDBServer.zip"));

        [Test]
        public void Brand_new_instances_use_the_RavenDB7_persister()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(ServiceControlNewInstance.CreateWithDefaultPersistence().PersistenceManifest.Name, Is.EqualTo("RavenDB7"));
                Assert.That(ServiceControlAuditNewInstance.CreateWithDefaultPersistence().PersistenceManifest.Name, Is.EqualTo("RavenDB7"));
            }
        }
    }
}
