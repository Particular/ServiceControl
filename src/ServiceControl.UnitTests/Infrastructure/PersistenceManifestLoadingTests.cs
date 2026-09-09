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
