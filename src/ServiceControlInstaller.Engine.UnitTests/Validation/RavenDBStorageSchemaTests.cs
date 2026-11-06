namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using Engine.Validation;
    using NUnit.Framework;

    [TestFixture]
    class RavenDBStorageSchemaTests
    {
        [Test]
        public void The_bundled_RavenDB_server_has_the_reviewed_storage_versions() =>
            Assert.That(RavenDBStorageSchema.Bundled, Is.EqualTo(new RavenDBStorageSchema(Server: 71_000, Configuration: 50_000, Documents: 72_002, LuceneIndex: 62_000, CoraxIndex: 62_000)),
                "The bundled RavenDB server changed a storage version, so upgrading an instance converts existing data again. " +
                "Measure how long the new conversion takes on a large database, adjust the warning in RavenDBStorageUpgrade if needed, then update the expected versions here.");

        [Test]
        public void Upgrading_from_RavenDB_6_2_converts_the_data() =>
            Assert.That(RavenDBStorageSchema.Bundled.RequiresDataUpgradeFrom(RavenDB62), Is.True);

        [Test]
        public void The_same_storage_versions_do_not_convert_the_data() =>
            Assert.That(RavenDB62.RequiresDataUpgradeFrom(RavenDB62), Is.False);

        [Test]
        public void A_change_to_server_or_configuration_storage_alone_does_not_convert_the_data() =>
            Assert.That(RavenDB62.RequiresDataUpgradeFrom(RavenDB62 with { Server = 50_000, Configuration = 40_000 }), Is.False);

        [Test]
        public void A_change_to_document_or_index_storage_converts_the_data()
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(RavenDB62.RequiresDataUpgradeFrom(RavenDB62 with { Documents = 61_000 }), Is.True);
                Assert.That(RavenDB62.RequiresDataUpgradeFrom(RavenDB62 with { LuceneIndex = 61_000 }), Is.True);
                Assert.That(RavenDB62.RequiresDataUpgradeFrom(RavenDB62 with { CoraxIndex = 61_000 }), Is.True);
            }
        }

        [Test]
        public void Reads_the_storage_versions_of_the_server_an_instance_runs()
        {
            var installPath = CreateTempDirectory();
            try
            {
                var serverPath = Path.Combine(installPath, "Persisters", "RavenDB", "RavenDBServer");
                Directory.CreateDirectory(serverPath);
                ExtractBundledServerAssembly(Path.Combine(serverPath, "Raven.Server.dll"));

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(RavenDBStorageSchema.ReadFromInstance(installPath), Is.EqualTo(RavenDBStorageSchema.Bundled));
                    Assert.That(RavenDBStorageSchema.RequiresDataUpgrade(installPath), Is.False);
                }
            }
            finally
            {
                Directory.Delete(installPath, recursive: true);
            }
        }

        [Test]
        public void An_instance_without_a_RavenDB_server_does_not_convert_data()
        {
            var installPath = CreateTempDirectory();
            try
            {
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(RavenDBStorageSchema.ReadFromInstance(installPath), Is.Null);
                    Assert.That(RavenDBStorageSchema.RequiresDataUpgrade(installPath), Is.False);
                }
            }
            finally
            {
                Directory.Delete(installPath, recursive: true);
            }
        }

        [Test]
        public void An_assembly_without_the_RavenDB_schema_versions_has_no_schema()
        {
            using var assembly = File.OpenRead(typeof(RavenDBStorageSchema).Assembly.Location);
            Assert.That(RavenDBStorageSchema.Read(assembly), Is.Null);
        }

        static void ExtractBundledServerAssembly(string destination)
        {
            using var zipStream = typeof(RavenDBStorageSchema).Assembly.GetManifestResourceStream("RavenDBServer.zip");
            using var archive = new ZipArchive(zipStream!, ZipArchiveMode.Read);
            archive.GetEntry("Raven.Server.dll")!.ExtractToFile(destination);
        }

        static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "RavenDBStorageSchemaTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        static readonly RavenDBStorageSchema RavenDB62 = new(Server: 62_000, Configuration: 50_000, Documents: 62_000, LuceneIndex: 62_000, CoraxIndex: 62_000);
    }
}
