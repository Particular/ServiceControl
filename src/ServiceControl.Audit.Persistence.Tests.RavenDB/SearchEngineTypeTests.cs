namespace ServiceControl.Audit.Persistence.Tests;

using System.Threading.Tasks;
using NUnit.Framework;
using Persistence.RavenDB;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;

[TestFixture]
class SearchEngineTypeTests : PersistenceTestFixture
{
    [Test]
    public async Task Free_text_search_should_be_on_using_corax_by_default()
    {
        var freeTextIndex = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(DatabaseSetup.MessagesViewIndexWithFullTextSearchIndexName));
        var nonFreeTextIndex = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexOperation(DatabaseSetup.MessagesViewIndexName));

        Assert.That(nonFreeTextIndex, Is.Null);
        Assert.That(freeTextIndex, Is.Not.Null);
        Assert.That(freeTextIndex.SearchEngineType, Is.EqualTo(SearchEngineType.Corax));
    }
}