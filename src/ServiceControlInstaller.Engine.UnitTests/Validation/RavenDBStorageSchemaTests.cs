namespace ServiceControlInstaller.Engine.UnitTests.Validation
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using Engine.Instances;
    using Engine.Validation;
    using NUnit.Framework;

    [TestFixture]
    class RavenDBStorageSchemaTests
    {
        [Test]
        public void The_bundled_RavenDB7_server_has_the_reviewed_storage_versions() =>
            Assert.That(RavenDBStorageSchema.Bundled(RavenDB7Persister), Is.EqualTo(RavenDB72),
                "The bundled RavenDB 7.2 server changed a storage version, so upgrading an instance on the RavenDB7 persister converts existing data again. " +
                "Measure how long the new conversion takes on a large database, adjust the warning in RavenDBStorageUpgrade if needed, then update the expected versions here.");

        [Test]
        public void The_bundled_RavenDB6_server_has_the_reviewed_storage_versions() =>
            Assert.That(RavenDBStorageSchema.Bundled(RavenDB6Persister), Is.EqualTo(RavenDB62),
                "The bundled RavenDB 6.2 server changed a storage version. The RavenDB persister stays on 6.2, so it should only change when RavenDB6Version does. " +
                "Check that upgrading an instance on the RavenDB persister still does not convert data, then update the expected versions here.");

        [Test]
        public void Every_persister_but_RavenDB7_gets_the_RavenDB_6_2_server_schema()
        {
            using (Assert.EnterMultipleScope())
            {
                foreach (var name in new[] { "RavenDB", "SQLServer", "PostgreSQL", "RavenDB35" })
                {
                    Assert.That(RavenDBStorageSchema.Bundled(new PersistenceManifest { Name = name }), Is.EqualTo(RavenDB62), name);
                }
            }
        }

        [Test]
        public void Upgrading_from_RavenDB_6_2_to_the_RavenDB7_server_converts_the_data() =>
            Assert.That(RavenDBStorageSchema.Bundled(RavenDB7Persister).RequiresDataUpgradeFrom(RavenDB62), Is.True);

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
        public void Reads_the_storage_versions_of_the_server_under_the_persister_an_instance_runs()
        {
            var installPath = CreateTempDirectory();
            try
            {
                InstallBundledServer(installPath, "RavenDB", RavenDBServerArchive);
                InstallBundledServer(installPath, "RavenDB7", RavenDB7ServerArchive);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(RavenDBStorageSchema.ReadFromInstance(installPath, RavenDB6Persister), Is.EqualTo(RavenDB62));
                    Assert.That(RavenDBStorageSchema.ReadFromInstance(installPath, RavenDB7Persister), Is.EqualTo(RavenDB72));
                }
            }
            finally
            {
                Directory.Delete(installPath, recursive: true);
            }
        }

        [Test]
        public void An_instance_on_RavenDB_with_its_6_2_server_does_not_convert_data_when_upgraded()
        {
            var installPath = CreateTempDirectory();
            try
            {
                InstallBundledServer(installPath, "RavenDB", RavenDBServerArchive);

                Assert.That(RavenDBStorageSchema.RequiresDataUpgrade(installPath, RavenDB6Persister, RavenDB6Persister), Is.False);
            }
            finally
            {
                Directory.Delete(installPath, recursive: true);
            }
        }

        [Test]
        public void An_instance_on_RavenDB7_with_its_7_2_server_does_not_convert_data_when_upgraded()
        {
            var installPath = CreateTempDirectory();
            try
            {
                InstallBundledServer(installPath, "RavenDB7", RavenDB7ServerArchive);

                Assert.That(RavenDBStorageSchema.RequiresDataUpgrade(installPath, RavenDB7Persister, RavenDB7Persister), Is.False);
            }
            finally
            {
                Directory.Delete(installPath, recursive: true);
            }
        }

        [Test]
        public void An_instance_on_RavenDB7_with_an_older_storage_version_server_converts_data_when_upgraded()
        {
            var installPath = CreateTempDirectory();
            try
            {
                // Stands in for a 7.x server with older storage versions than the bundled one.
                InstallBundledServer(installPath, "RavenDB7", RavenDBServerArchive);

                Assert.That(RavenDBStorageSchema.RequiresDataUpgrade(installPath, RavenDB7Persister, RavenDB7Persister), Is.True);
            }
            finally
            {
                Directory.Delete(installPath, recursive: true);
            }
        }

        [Test]
        public void Moving_an_instance_from_RavenDB_to_RavenDB7_converts_the_data()
        {
            var installPath = CreateTempDirectory();
            try
            {
                InstallBundledServer(installPath, "RavenDB", RavenDBServerArchive);

                Assert.That(RavenDBStorageSchema.RequiresDataUpgrade(installPath, RavenDB6Persister, RavenDB7Persister), Is.True);
            }
            finally
            {
                Directory.Delete(installPath, recursive: true);
            }
        }

        [Test]
        public void The_RavenDB7_server_folder_does_not_stand_in_for_the_server_a_RavenDB_instance_runs()
        {
            var installPath = CreateTempDirectory();
            try
            {
                InstallBundledServer(installPath, "RavenDB7", RavenDB7ServerArchive);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(RavenDBStorageSchema.ReadFromInstance(installPath, RavenDB6Persister), Is.Null);
                    Assert.That(RavenDBStorageSchema.RequiresDataUpgrade(installPath, RavenDB6Persister, RavenDB7Persister), Is.False);
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
                    Assert.That(RavenDBStorageSchema.ReadFromInstance(installPath, RavenDB6Persister), Is.Null);
                    Assert.That(RavenDBStorageSchema.RequiresDataUpgrade(installPath, RavenDB6Persister, RavenDB6Persister), Is.False);
                    Assert.That(RavenDBStorageSchema.RequiresDataUpgrade(installPath, RavenDB7Persister, RavenDB7Persister), Is.False);
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

        internal static void InstallBundledServer(string installPath, string persisterFolder, string archiveResourceName)
        {
            var serverPath = Path.Combine(installPath, "Persisters", persisterFolder, "RavenDBServer");
            Directory.CreateDirectory(serverPath);

            using var zipStream = typeof(RavenDBStorageSchema).Assembly.GetManifestResourceStream(archiveResourceName);
            using var archive = new ZipArchive(zipStream!, ZipArchiveMode.Read);
            archive.GetEntry("Raven.Server.dll")!.ExtractToFile(Path.Combine(serverPath, "Raven.Server.dll"));
        }

        static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "RavenDBStorageSchemaTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        const string RavenDBServerArchive = "RavenDBServer.zip";
        const string RavenDB7ServerArchive = "RavenDB7Server.zip";

        static readonly PersistenceManifest RavenDB6Persister = new() { Name = StorageEngineNames.RavenDB };
        static readonly PersistenceManifest RavenDB7Persister = new() { Name = StorageEngineNames.RavenDB7 };

        static readonly RavenDBStorageSchema RavenDB62 = new(Server: 62_000, Configuration: 50_000, Documents: 62_000, LuceneIndex: 62_000, CoraxIndex: 62_000);
        static readonly RavenDBStorageSchema RavenDB72 = new(Server: 71_000, Configuration: 50_000, Documents: 72_002, LuceneIndex: 62_000, CoraxIndex: 62_000);
    }
}
