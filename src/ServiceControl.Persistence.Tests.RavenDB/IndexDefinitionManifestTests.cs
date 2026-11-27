namespace ServiceControl.Persistence.Tests.RavenDB;

using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.RavenDB;
using ServiceControlInstaller.Engine.Validation;
using TestHelper;

// The installer compares index-definitions.manifest with the index definitions on the server before an upgrade.
[TestFixture]
class IndexDefinitionManifestTests
{
    [Test]
    public void Manifest_is_current()
    {
        var rendered = IndexDefinitionManifest.Render(IndexDeployment.FindIndexes(typeof(DatabaseSetup).Assembly));

        if (IndexDefinitionManifest.NormalizeLineEndings(File.ReadAllText(ManifestPath)) != rendered)
        {
            var receivedPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, IndexDefinitionManifest.FileName + ".received");
            File.WriteAllText(receivedPath, rendered);
            Assert.Fail($"{IndexDefinitionManifest.FileName} in ServiceControl.Persistence.RavenDB does not match the index definitions. Replace it with {receivedPath}.");
        }
    }

    [Test]
    public async Task Deployed_indexes_have_no_changes()
    {
        var (changes, _) = await DeployAndDetect(File.ReadAllText(ManifestPath));

        Assert.That(changes.Changes, Is.Empty);
    }

    [Test]
    public async Task Changed_map_is_reported()
    {
        var (changes, database) = await DeployAndDetect(File.ReadAllText(ManifestPath).Replace("    Id = ", "    IdChanged = "));

        Assert.That(changes.Changes, Is.Not.Empty);
        Assert.That(changes.Changes.Select(change => change.Database).Distinct(), Is.EqualTo(new[] { database }));
        Assert.That(changes.Changes.SelectMany(change => change.Differences), Does.Contain("Maps"));
        Assert.That(changes.Changes.All(change => change.Entries == 0 && !change.IsLocked), Is.True);
    }

    static async Task<(RavenDBIndexChanges Changes, string Database)> DeployAndDetect(string manifest)
    {
        await using var database = await ScratchDatabase.Create();
        var store = database.Store;

        var indexes = IndexDeployment.FindIndexes(typeof(DatabaseSetup).Assembly).ToList();
        await IndexDeployment.CreateIndexesAsync(indexes, store);

        using var http = new HttpClient();
        var changes = await RavenDBIndexChanges.Detect("instance", RavenDBIndexDefinitions.Parse(manifest), (path, token) => http.GetStringAsync(store.Urls[0] + path, token));

        // The shared server also holds the databases of other tests.
        return (new RavenDBIndexChanges { InstanceName = changes.InstanceName, Changes = changes.Changes.Where(change => change.Database == store.Database).ToList() }, store.Database);
    }

    static string ManifestPath => Path.Combine(TestContext.CurrentContext.TestDirectory, IndexDefinitionManifest.FileName);
}
