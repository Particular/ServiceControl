namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;

    [TestFixture]
    public class PersistenceManifestLibraryTests
    {
        const string persistenceName = "RavenDB5";
        const string persistenceType = "ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb5";
        const string persistenceFolder = "RavenDB5";

        [Test]
        public void Should_find_persistence_type_by_name()
        {
            var _persistenceType = PersistenceManifestLibrary.Find(persistenceName);

            Assert.AreEqual(persistenceType, _persistenceType);
        }

        [Test]
        public void Should_find_tpersistence_type_by_type()
        {
            var _persistenceType = PersistenceManifestLibrary.Find(persistenceType);

            Assert.AreEqual(persistenceType, _persistenceType);
        }

        [Test]
        public void Should_return_persistence_type_passed_in_if_not_found()
        {
            var fakePersistenceType = "My.fake.persistence, fakeTransportAssembly";
            var _persistenceType = PersistenceManifestLibrary.Find(fakePersistenceType);

            Assert.AreEqual(fakePersistenceType, _persistenceType);
        }

        [Test]
        public void Should_find_persistence_type_folder_by_name()
        {
            var _persistenceTypeFolder = PersistenceManifestLibrary.GetPersistenceFolder(persistenceName);

            Assert.AreEqual(persistenceFolder, _persistenceTypeFolder);
        }

        [Test]
        public void Should_find_tpersistence_type_folder_by_type()
        {
            var _persistenceTypeFolder = PersistenceManifestLibrary.GetPersistenceFolder(persistenceType);

            Assert.AreEqual(persistenceFolder, _persistenceTypeFolder);
        }

        [Test]
        public void Should_return_null_for_not_found_persistence_type()
        {
            var fakePersistenceType = "My.fake.persistence, fakeTransportAssembly";
            var _persistenceTypeFolder = PersistenceManifestLibrary.GetPersistenceFolder(fakePersistenceType);

            Assert.IsNull(_persistenceTypeFolder);
        }

        [Test]
        public void All_types_defined_in_manifest_files_exist_in_specified_assembly()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var appDirectory = Path.GetDirectoryName(assemblyLocation);
            PersistenceManifestLibrary.GetPersistenceFolder("dummy"); //to initialise the collection
            PersistenceManifestLibrary.PersistenceManifests.ForEach(p =>
            {
                var persistenceFolder = PersistenceManifestLibrary.GetPersistenceFolder(p.Name);
                var subFolderPath = Path.Combine(appDirectory, "Persisters", persistenceFolder);
                var assemblyName = p.TypeName.Split(',')[1].Trim();
                var assembly = TryLoadTypeFromSubdirectory(subFolderPath, assemblyName);

                Assert.IsNotNull(assembly, $"Could not load assembly {assemblyName}");

                //NOTE not checking namespace here as it doesn't match for RavenDb5
                //Assert.IsTrue(assembly.GetTypes().Any(a => a.FullName == p.TypeName.Split(',').FirstOrDefault() && a.Namespace == assemblyName), $"Persistence type {p.TypeName} not found in assembly {assemblyName}");
                Assert.IsTrue(assembly.GetTypes().Any(a => a.FullName == p.TypeName.Split(',').FirstOrDefault()), $"Persistence type {p.TypeName} not found in assembly {assemblyName}");
            });
        }

        Assembly TryLoadTypeFromSubdirectory(string subFolderPath, string requestingName)
        {
            //look into any subdirectory
            var file = Directory.EnumerateFiles(subFolderPath, requestingName + ".dll", SearchOption.AllDirectories).SingleOrDefault();
            if (file != null)
            {
                return Assembly.LoadFrom(file);
            }

            return null;
        }
    }
}