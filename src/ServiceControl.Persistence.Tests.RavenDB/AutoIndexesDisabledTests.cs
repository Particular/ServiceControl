namespace ServiceControl.Persistence.Tests.RavenDB;

using System.Linq;
using System.Threading.Tasks;
using MessageFailures;
using NUnit.Framework;
using Raven.Client.Documents;

// The shared embedded server runs with Indexing.DisableQueryOptimizerGeneratedIndexes, so a query that would create
// an auto index fails the test instead.
[TestFixture]
class AutoIndexesDisabledTests : RavenPersistenceTestBase
{
    [Test]
    public async Task Dynamic_query_should_throw()
    {
        using var session = DocumentStore.OpenAsyncSession();

        var query = session.Query<FailedMessage>().Where(message => message.UniqueMessageId == "some-id");

        Assert.That(async () => await query.ToListAsync(), Throws.Exception.With.Message.Contains("DisableQueryOptimizerGeneratedIndexes"));
    }
}
