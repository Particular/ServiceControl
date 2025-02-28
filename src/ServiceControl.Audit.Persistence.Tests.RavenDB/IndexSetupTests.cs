namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Threading.Tasks;
using global::ServiceControl.Audit.Persistence.RavenDB;
using NUnit.Framework;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;

[TestFixture]
class IndexSetupTests : PersistenceTestFixture
{
    [Test]
    public async Task Corax_should_the_defaul_search_engine_type()
    {
        var indexes = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexesOperation(0, int.MaxValue));

        foreach (var index in indexes)
        {
            var indexStats = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(DatabaseSetup.MessagesViewIndexWithFulltextSearchName));
            Assert.That(indexStats.SearchEngineType, Is.EqualTo(SearchEngineType.Corax), $"{index.Name} is not using Corax");
        }
    }

    [Test]
    public async Task Free_text_search_index_should_be_used_by_default()
    {
        var freeTextIndex = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexOperation(DatabaseSetup.MessagesViewIndexWithFulltextSearchName));
        var nonFreeTextIndex = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexOperation(DatabaseSetup.MessagesViewIndexName));

        Assert.That(nonFreeTextIndex, Is.Null);
        Assert.That(freeTextIndex, Is.Not.Null);
    }
}