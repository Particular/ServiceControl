namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;
    using ServiceControl.Infrastructure;

    [TestFixture]
    public class PersistenceManifestLibraryTests
    {
        const string persistenceName = "RavenDB";
        const string persistenceType = "ServiceControl.Audit.Persistence.RavenDB.RavenPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDB";
        const string persistenceAlias = "ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb5";

        [Test]
        public void Should_find_persistence_manifest_by_name()
        {
            var persistenceManifest = PersistenceManifestLibrary.Find(persistenceName);

            Assert.That(persistenceManifest.Name, Is.EqualTo(persistenceName));
        }

        [Test]
        public void Should_find_persistence_manifest_by_type()
        {
            var persistenceManifest = PersistenceManifestLibrary.Find(persistenceType);

            Assert.That(persistenceManifest.TypeName, Is.EqualTo(persistenceType));
        }

        [Test]
        public void Should_return_null_for_not_found_persistence_type()
        {
            var fakePersistenceType = "My.fake.persistence, fakeTransportAssembly";
            var persistenceManifest = PersistenceManifestLibrary.Find(fakePersistenceType);

            Assert.That(persistenceManifest, Is.Null);
        }

        [Test]
        public void Should_find_transport_manifest_by_alias()
        {
            var persistenceManifest = PersistenceManifestLibrary.Find(persistenceAlias);

            Assert.That(persistenceManifest, Is.Not.Null);
            Assert.That(persistenceManifest.Aliases, Does.Contain(persistenceAlias));
        }

        [Test]
        public void Should_find_the_legacy_persistence_manifest_by_name_alias_and_type()
        {
            foreach (var candidate in new[] { "RavenDB", "ravendb", "RavenDB6", persistenceAlias, persistenceType })
            {
                Assert.That(PersistenceManifestLibrary.Find(candidate)?.DisplayName, Is.EqualTo("RavenDB 6"), candidate);
            }
        }

        [Test]
        public void Should_find_the_modern_persistence_manifest_only_by_the_RavenDB7_name()
        {
            Assert.That(PersistenceManifestLibrary.Find("RavenDB7")?.DisplayName, Is.EqualTo("RavenDB 7"));
        }

        [Test]
        public void All_types_defined_in_manifest_files_exist_in_specified_assembly()
        {
            var count = 0;

            foreach (var definition in PersistenceManifestLibrary.PersistenceManifests)
            {
                count++;
                var assemblyName = definition.TypeName.Split(',')[1].Trim();
                var assemblyFile = Path.Combine(definition.Location, assemblyName + ".dll");
                var assembly = new PluginAssemblyLoadContext(assemblyFile).LoadFromAssemblyPath(assemblyFile);

                Assert.That(assembly, Is.Not.Null, $"Could not load assembly {assemblyName}");

                var fullName = definition.TypeName.Split(',').FirstOrDefault();
                var foundType = assembly.GetType(fullName);

                Assert.That(foundType, Is.Not.Null, $"Persistence type {definition.TypeName} not found in assembly {assemblyName}");
            }

            Assert.That(count, Is.Not.Zero, "No persistence manifests found.");
        }
    }

    [TestFixture]
    public class PersistenceManifestLoadingTests
    {
        const string RavenDB = """
                               {
                                 "Name": "RavenDB",
                                 "DisplayName": "RavenDB",
                                 "Description": "RavenDB ServiceControl Audit persister",
                                 "AssemblyName": "ServiceControl.Audit.Persistence.RavenDB",
                                 "TypeName": "ServiceControl.Audit.Persistence.RavenDB.RavenPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDB"
                               }
                               """;

        // Ships so that ServiceControl Management can describe pre-v5 instances, and has no assembly left to name
        const string RavenDB35 = """
                                 {
                                   "Name": "RavenDB35",
                                   "IsSupported": false,
                                   "DisplayName": "RavenDB 3.5 (Legacy)",
                                   "Description": "RavenDB 3.5 (Legacy) ServiceControl Audit persister"
                                 }
                                 """;

        [Test]
        public void Legacy_manifest_without_an_assembly_does_not_hide_the_persisters_after_it()
        {
            var manifests = LoadFrom(new()
            {
                ["RavenDB"] = RavenDB,
                ["RavenDB35"] = RavenDB35
            });

            Assert.That(manifests.Select(m => m.Name), Is.EquivalentTo(["RavenDB", "RavenDB35"]));
        }

        [Test]
        public void Unreadable_manifest_does_not_hide_the_persisters_after_it()
        {
            var manifests = LoadFrom(new()
            {
                ["Corrupt"] = "{ this is not json",
                ["RavenDB"] = RavenDB
            });

            Assert.That(manifests.Select(m => m.Name), Is.EqualTo(["RavenDB"]));
        }

        static List<PersistenceManifest> LoadFrom(Dictionary<string, string> persisters)
        {
            var installDirectory = Path.Combine(Path.GetTempPath(), TestContext.CurrentContext.Test.ID);

            foreach (var (persister, manifest) in persisters)
            {
                var persisterDirectory = Path.Combine(installDirectory, "Persisters", persister);
                Directory.CreateDirectory(persisterDirectory);
                File.WriteAllText(Path.Combine(persisterDirectory, "persistence.manifest"), manifest);
            }

            try
            {
                return PersistenceManifestLibrary.LoadManifests(Directory.EnumerateFiles(installDirectory, "persistence.manifest", SearchOption.AllDirectories));
            }
            finally
            {
                Directory.Delete(installDirectory, true);
            }
        }
    }

    [TestFixture]
    public class PersistenceManifestFindTests
    {
        const string RavenDBTypeName = "ServiceControl.Audit.Persistence.RavenDB.RavenPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDB";
        const string RavenDb5Alias = "ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb5";

        static readonly PersistenceManifest RavenDB6 = new()
        {
            Name = "RavenDB",
            DisplayName = "RavenDB 6",
            Description = "RavenDB 6 ServiceControl Audit persister",
            TypeName = RavenDBTypeName,
            Aliases = ["RavenDB6", RavenDBTypeName, RavenDb5Alias]
        };

        static readonly PersistenceManifest RavenDB7 = new()
        {
            Name = "RavenDB7",
            DisplayName = "RavenDB 7",
            Description = "RavenDB 7 ServiceControl Audit persister",
            TypeName = RavenDBTypeName
        };

        static IEnumerable<TestCaseData> ManifestOrders()
        {
            yield return new TestCaseData((object)new[] { RavenDB6, RavenDB7 }).SetName("Legacy_first");
            yield return new TestCaseData((object)new[] { RavenDB7, RavenDB6 }).SetName("Modern_first");
        }

        [TestCaseSource(nameof(ManifestOrders))]
        public void Name_alias_and_type_name_of_the_legacy_persister_find_the_legacy_manifest(PersistenceManifest[] manifests)
        {
            foreach (var persistenceType in new[] { "RavenDB", "ravendb", "RavenDB6", "ravendb6", RavenDBTypeName, RavenDb5Alias })
            {
                Assert.That(PersistenceManifestLibrary.Find(manifests, persistenceType), Is.SameAs(RavenDB6), persistenceType);
            }
        }

        [TestCaseSource(nameof(ManifestOrders))]
        public void Only_the_RavenDB7_name_finds_the_modern_manifest(PersistenceManifest[] manifests)
        {
            Assert.That(PersistenceManifestLibrary.Find(manifests, "RavenDB7"), Is.SameAs(RavenDB7));
            Assert.That(PersistenceManifestLibrary.Find(manifests, "ravendb7"), Is.SameAs(RavenDB7));
        }

        [TestCaseSource(nameof(ManifestOrders))]
        public void Unknown_persistence_type_finds_no_manifest(PersistenceManifest[] manifests) =>
            Assert.That(PersistenceManifestLibrary.Find(manifests, "My.fake.persistence, fakePersistenceAssembly"), Is.Null);
    }
}
