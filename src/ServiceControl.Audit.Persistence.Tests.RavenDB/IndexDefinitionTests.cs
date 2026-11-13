namespace ServiceControl.Audit.Persistence.Tests;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.Approvals;
using Persistence.RavenDB;
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

        // The database has no search engine setting, so deploying sets Lucene on every definition.
        await IndexDeployment.CreateIndexesAsync(indexes, Store, cancellationToken: TestTimeoutCancellationToken);

        Approver.Verify(IndexDefinitionText.Render(indexes));
    }
}
