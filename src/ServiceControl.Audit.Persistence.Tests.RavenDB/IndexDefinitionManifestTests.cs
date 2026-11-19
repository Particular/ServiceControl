namespace ServiceControl.Audit.Persistence.Tests;

using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using Persistence.RavenDB;
using ServiceControl.RavenDB;
using ServiceControlInstaller.Engine.Validation;
using TestHelper;

// The installer compares index-definitions.manifest with the index definitions on the server before an upgrade.
[TestFixture]
class IndexDefinitionManifestTests : CleanDatabaseFixture
{
    [Test]
    public void Manifest_is_current()
    {
        var rendered = IndexDefinitionManifest.Render(IndexDeployment.FindIndexes(typeof(DatabaseSetup).Assembly));

        if (IndexDefinitionManifest.NormalizeLineEndings(File.ReadAllText(ManifestPath)) != rendered)
        {
            var receivedPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, IndexDefinitionManifest.FileName + ".received");
            File.WriteAllText(receivedPath, rendered);
            Assert.Fail($"{IndexDefinitionManifest.FileName} in ServiceControl.Audit.Persistence.RavenDB does not match the index definitions. Replace it with {receivedPath}.");
        }
    }

    [Test]
    public async Task Deployed_indexes_have_no_changes()
    {
        var changes = await DeployAndDetect(File.ReadAllText(ManifestPath));

        Assert.That(changes.Changes, Is.Empty);
    }

    [Test]
    public async Task Changed_map_is_reported()
    {
        var changes = await DeployAndDetect(File.ReadAllText(ManifestPath).Replace("ProcessedAt = ", "ProcessedAtChanged = "));

        Assert.That(changes.Changes, Is.Not.Empty);
        Assert.That(changes.Changes.Select(change => change.Database).Distinct(), Is.EqualTo(new[] { Store.Database }));
        Assert.That(changes.Changes.SelectMany(change => change.Differences).Distinct(), Is.EqualTo(new[] { "Maps" }));
        Assert.That(changes.Changes.All(change => change.Entries == 0 && !change.IsLocked), Is.True);
    }

    async Task<RavenDBIndexChanges> DeployAndDetect(string manifest)
    {
        var indexes = IndexDeployment.FindIndexes(typeof(DatabaseSetup).Assembly).ToList();
        await IndexDeployment.CreateIndexesAsync(indexes, Store, cancellationToken: TestTimeoutCancellationToken);

        using var http = new HttpClient();
        var changes = await RavenDBIndexChanges.Detect("instance", RavenDBIndexDefinitions.Parse(manifest), (path, token) => http.GetStringAsync(Store.Urls[0] + path, token), TestTimeoutCancellationToken);

        // The shared server also holds the databases of other tests.
        return new RavenDBIndexChanges { InstanceName = changes.InstanceName, Changes = changes.Changes.Where(change => change.Database == Store.Database).ToList() };
    }

    static string ManifestPath => Path.Combine(TestContext.CurrentContext.TestDirectory, IndexDefinitionManifest.FileName);
}
