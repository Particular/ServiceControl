namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;

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
            Assert.That(persistenceManifest.Aliases[0], Is.EqualTo(persistenceAlias));
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
                var assembly = Assembly.LoadFrom(assemblyFile);

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

        const string InMemory = """
                                {
                                  "Name": "InMemory",
                                  "DisplayName": "In-memory",
                                  "Description": "InMemory ServiceControl Audit persister",
                                  "AssemblyName": "ServiceControl.Audit.Persistence.InMemory",
                                  "TypeName": "ServiceControl.Audit.Persistence.InMemory.InMemoryPersistenceConfiguration, ServiceControl.Audit.Persistence.InMemory"
                                }
                                """;

        [Test]
        public void Legacy_manifest_without_an_assembly_does_not_hide_the_persisters_after_it()
        {
            var manifests = LoadFrom(new()
            {
                ["RavenDB"] = RavenDB,
                ["RavenDB35"] = RavenDB35,
                ["InMemory"] = InMemory
            });

            Assert.That(manifests.Select(m => m.Name), Is.EquivalentTo(["RavenDB", "RavenDB35", "InMemory"]));
        }

        [Test]
        public void Unreadable_manifest_does_not_hide_the_persisters_after_it()
        {
            var manifests = LoadFrom(new()
            {
                ["Corrupt"] = "{ this is not json",
                ["InMemory"] = InMemory
            });

            Assert.That(manifests.Select(m => m.Name), Is.EqualTo(["InMemory"]));
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
}