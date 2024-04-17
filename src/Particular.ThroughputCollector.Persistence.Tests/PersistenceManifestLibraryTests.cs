namespace Particular.ThroughputCollector.Persistence.Tests
{
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;

    [TestFixture]
    public class PersistenceManifestLibraryTests
    {
        const string persistenceName = "RavenDB";
        const string persistenceType = "Particular.ThroughputCollector.Persistence.RavenDb.RavenPersistenceConfiguration, Particular.ThroughputCollector.Persistence.RavenDb";

        [Test]
        public void Should_find_persistence_type_by_name()
        {
            var _persistenceType = PersistenceManifestLibrary.Find(persistenceName).TypeName;

            Assert.That(_persistenceType, Is.EqualTo(persistenceType));
        }

        [Test]
        public void Should_find_persistence_type_by_type()
        {
            var _persistenceType = PersistenceManifestLibrary.Find(persistenceType).TypeName;

            Assert.That(_persistenceType, Is.EqualTo(persistenceType));
        }

        [Test]
        public void Should_find_persistence_type_folder_by_name()
        {
            var _persistenceTypeFolder = PersistenceManifestLibrary.Find(persistenceType).Location;

            Assert.That(_persistenceTypeFolder, Is.Not.Null);
        }

        [Test]
        public void Should_find_persistence_type_folder_by_type()
        {
            var _persistenceTypeFolder = PersistenceManifestLibrary.Find(persistenceType).Location;

            Assert.That(_persistenceTypeFolder, Is.Not.Null);
        }

        [Test]
        public void All_types_defined_in_manifest_files_exist_in_specified_assembly()
        {
            var count = 0;

            foreach (var definition in PersistenceManifestLibrary.PersistenceManifests)
            {
                count++;
                var persistenceFolder = definition.Location;
                var assemblyName = definition.TypeName.Split(',')[1].Trim();
                var assemblyFile = Path.Combine(persistenceFolder, assemblyName + ".dll");
                var assembly = Assembly.LoadFrom(assemblyFile);

                Assert.That(assembly, Is.Not.Null, $"Could not load assembly {assemblyName}");

                var fullName = definition.TypeName.Split(',').FirstOrDefault();
                var foundType = assembly.GetType(fullName);

                Assert.That(foundType, Is.Not.Null, $"Persistence type {definition.TypeName} not found in assembly {assemblyName}");
            }

            Assert.That(count, Is.Not.Zero, "No persistence manifests found.");
        }
    }
}