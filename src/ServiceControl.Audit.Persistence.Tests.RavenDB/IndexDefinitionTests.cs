namespace ServiceControl.Audit.Persistence.Tests;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.Approvals;
using Persistence.RavenDB;
using Raven.Client.Documents.Indexes;
using Raven.Client.ServerWide;
using ServiceControl.RavenDB;
using TestHelper;

[TestFixture]
class IndexDefinitionTests : CleanDatabaseFixture
{
    // A definition that differs from the one on the server makes RavenDB rebuild the index side by side, which takes
    // days on a large audit database. Record the change in the upgrade guide before approving it, see docs/testing.md.
    [Test]
    public async Task VerifyGeneratedDefinitions()
    {
        var indexes = IndexDeployment.FindIndexes(typeof(DatabaseSetup).Assembly).ToList();

        // On a Lucene database the indexes inherit the search engine, so deploying leaves no setting on the definition.
        await IndexDeployment.CreateIndexesAsync(indexes, Store, cancellationToken: TestTimeoutCancellationToken);

        Approver.Verify(IndexDefinitionText.Render(indexes));
    }

    // The search engine settings that DatabaseSetup uses for new databases.
    protected override void ConfigureDatabase(DatabaseRecord databaseRecord)
    {
        databaseRecord.Settings["Indexing.Auto.SearchEngineType"] = nameof(SearchEngineType.Lucene);
        databaseRecord.Settings["Indexing.Static.SearchEngineType"] = nameof(SearchEngineType.Lucene);
    }
}
