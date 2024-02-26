namespace Particular.ThroughputCollector.Persistence.Tests
{
    //using System.IO;
    //using System.Linq;
    //using System.Reflection;
    //using NUnit.Framework;

    //[TestFixture]
    //public class PersistenceManifestLibraryTests
    //{
    //    const string persistenceName = "RavenDB";
    //    const string persistenceType = "Particular.ThroughputCollector.Persistence.RavenDB.RavenPersistenceConfiguration, Particular.ThroughputCollector.Persistence.RavenDB";

    //    [Test]
    //    public void Should_find_persistence_type_by_name()
    //    {
    //        var _persistenceType = PersistenceManifestLibrary.Find(persistenceName);

    //        Assert.AreEqual(persistenceType, _persistenceType);
    //    }

    //    [Test]
    //    public void Should_find_persistence_type_by_type()
    //    {
    //        var _persistenceType = PersistenceManifestLibrary.Find(persistenceType);

    //        Assert.AreEqual(persistenceType, _persistenceType);
    //    }

    //    [Test]
    //    public void Should_return_persistence_type_passed_in_if_not_found()
    //    {
    //        var fakePersistenceType = "My.fake.persistence, fakeTransportAssembly";
    //        var _persistenceType = PersistenceManifestLibrary.Find(fakePersistenceType);

    //        Assert.AreEqual(fakePersistenceType, _persistenceType);
    //    }

    //    [Test]
    //    public void Should_find_persistence_type_folder_by_name()
    //    {
    //        var _persistenceTypeFolder = PersistenceManifestLibrary.GetPersistenceFolder(persistenceName);

    //        Assert.IsNotNull(_persistenceTypeFolder);
    //    }

    //    [Test]
    //    public void Should_find_persistence_type_folder_by_type()
    //    {
    //        var _persistenceTypeFolder = PersistenceManifestLibrary.GetPersistenceFolder(persistenceType);

    //        Assert.IsNotNull(_persistenceTypeFolder);
    //    }

    //    [Test]
    //    public void Should_return_null_for_not_found_persistence_type()
    //    {
    //        var fakePersistenceType = "My.fake.persistence, fakeTransportAssembly";
    //        var _persistenceTypeFolder = PersistenceManifestLibrary.GetPersistenceFolder(fakePersistenceType);

    //        Assert.IsNull(_persistenceTypeFolder);
    //    }

    //    [Test]
    //    public void All_types_defined_in_manifest_files_exist_in_specified_assembly()
    //    {
    //        var count = 0;

    //        foreach (var definition in PersistenceManifestLibrary.PersistenceManifests)
    //        {
    //            count++;
    //            var persistenceFolder = PersistenceManifestLibrary.GetPersistenceFolder(definition.Name);
    //            var assemblyName = definition.TypeName.Split(',')[1].Trim();
    //            var assemblyFile = Path.Combine(persistenceFolder, assemblyName + ".dll");
    //            var assembly = Assembly.LoadFrom(assemblyFile);

    //            Assert.IsNotNull(assembly, $"Could not load assembly {assemblyName}");

    //            var fullName = definition.TypeName.Split(',').FirstOrDefault();
    //            var foundType = assembly.GetType(fullName);

    //            Assert.IsNotNull(foundType, $"Persistence type {definition.TypeName} not found in assembly {assemblyName}");
    //        }

    //        Assert.NotZero(count, "No persistence manifests found.");
    //    }
    //}
}