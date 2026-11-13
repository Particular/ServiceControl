namespace ServiceControl.Persistence.Tests.RavenDB;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.Approvals;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.RavenDB;
using TestHelper;

// RavenDB rebuilds an index side by side when the definition the client sends differs from the stored one, which
// takes days on a large database. Record the change in the upgrade guide before approving it, see docs/testing.md.
[TestFixture]
class IndexDefinitionTests
{
    [Test]
    public async Task Primary_index_definitions_are_unchanged()
    {
        await using var database = await ScratchDatabase.Create();

        var indexes = IndexDeployment.FindIndexes(typeof(DatabaseSetup).Assembly).ToList();

        // On a Lucene database the indexes inherit the search engine, so deploying leaves no setting on the definition.
        await IndexDeployment.CreateIndexesAsync(indexes, database.Store);

        Approver.Verify(IndexDefinitionText.Render(indexes));
    }
}
