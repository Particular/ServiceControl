namespace ServiceControl.UnitTests.Infrastructure;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ServiceControl.Persistence;

[TestFixture]
public class PersistenceManifestLoadingTests
{
    const string RavenDB = """
                           {
                             "Name": "RavenDB",
                             "DisplayName": "RavenDB",
                             "Description": "RavenDB ServiceControl persister",
                             "AssemblyName": "ServiceControl.Persistence.RavenDB",
                             "TypeName": "ServiceControl.Persistence.RavenDB.RavenPersistenceConfiguration, ServiceControl.Persistence.RavenDB"
                           }
                           """;

    // Ships so that ServiceControl Management can describe pre-v5 instances, and has no assembly left to name
    const string RavenDB35 = """
                             {
                               "Name": "RavenDB35",
                               "IsSupported": false,
                               "DisplayName": "RavenDB 3.5 (Legacy)",
                               "Description": "RavenDB 3.5 (Legacy) ServiceControl persister"
                             }
                             """;

    const string SqlServer = """
                             {
                               "Name": "SQLServer",
                               "DisplayName": "SQL Server",
                               "Description": "SQL Server ServiceControl persister",
                               "AssemblyName": "ServiceControl.Persistence.EFCore.SqlServer",
                               "TypeName": "ServiceControl.Persistence.EFCore.SqlServer.SqlServerPersistenceConfiguration, ServiceControl.Persistence.EFCore.SqlServer"
                             }
                             """;

    [Test]
    public void Legacy_manifest_without_an_assembly_does_not_hide_the_persisters_after_it()
    {
        var manifests = LoadFrom(new()
        {
            ["RavenDB"] = RavenDB,
            ["RavenDB35"] = RavenDB35,
            ["SQLServer"] = SqlServer
        });

        Assert.That(manifests.Select(m => m.Name), Is.EquivalentTo(["RavenDB", "RavenDB35", "SQLServer"]));
    }

    [Test]
    public void Unreadable_manifest_does_not_hide_the_persisters_after_it()
    {
        var manifests = LoadFrom(new()
        {
            ["Corrupt"] = "{ this is not json",
            ["SQLServer"] = SqlServer
        });

        Assert.That(manifests.Select(m => m.Name), Is.EqualTo(["SQLServer"]));
    }

    static List<PersistenceManifest> LoadFrom(Dictionary<string, string> persisters)
    {
        var installDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, TestContext.CurrentContext.Test.ID);

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
    const string RavenDBTypeName = "ServiceControl.Persistence.RavenDB.RavenPersistenceConfiguration, ServiceControl.Persistence.RavenDB";

    static readonly PersistenceManifest RavenDB6 = new()
    {
        Name = "RavenDB",
        DisplayName = "RavenDB 6",
        Description = "RavenDB 6 ServiceControl persister",
        TypeName = RavenDBTypeName,
        Aliases = ["RavenDB6", RavenDBTypeName]
    };

    static readonly PersistenceManifest RavenDB7 = new()
    {
        Name = "RavenDB7",
        DisplayName = "RavenDB 7",
        Description = "RavenDB 7 ServiceControl persister",
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
        foreach (var persistenceType in new[] { "RavenDB", "ravendb", "RavenDB6", "ravendb6", RavenDBTypeName })
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

    [Test]
    public void Type_name_finds_a_manifest_when_no_name_or_alias_matches()
    {
        var type = typeof(PersistenceManifestFindTests);
        var manifest = new PersistenceManifest
        {
            Name = "Test",
            DisplayName = "Test",
            Description = "Test persister",
            TypeName = $"{type.FullName}, {type.Assembly.GetName().Name}"
        };

        Assert.That(PersistenceManifestLibrary.Find([RavenDB6, manifest], type.AssemblyQualifiedName!), Is.SameAs(manifest));
    }

    [Test]
    public void Shipped_manifests_resolve_the_same_way()
    {
        var manifests = PersistenceManifestLibrary.LoadManifests(DevelopmentPersistenceLocations.ManifestFiles);

        Assert.That(PersistenceManifestLibrary.Find(manifests, "RavenDB")?.DisplayName, Is.EqualTo("RavenDB 6"));
        Assert.That(PersistenceManifestLibrary.Find(manifests, "RavenDB6")?.DisplayName, Is.EqualTo("RavenDB 6"));
        Assert.That(PersistenceManifestLibrary.Find(manifests, RavenDBTypeName)?.DisplayName, Is.EqualTo("RavenDB 6"));
        Assert.That(PersistenceManifestLibrary.Find(manifests, "RavenDB7")?.DisplayName, Is.EqualTo("RavenDB 7"));
    }
}
