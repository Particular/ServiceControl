namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Persistence.RavenDB;
using Persistence.RavenDB.Indexes;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Exceptions.Documents.Indexes;

[TestFixture]
class IndexSetupTests : PersistenceTestFixture
{
    [Test]
    public async Task Corax_should_be_the_default_search_engine_type()
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

    [Test]
    public async Task Free_text_search_index_can_be_opted_out_from()
    {
        await DatabaseSetup.CreateIndexes(configuration.DocumentStore, false, TestTimeoutCancellationToken);

        var freeTextIndex = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexOperation(DatabaseSetup.MessagesViewIndexWithFulltextSearchName));
        var nonFreeTextIndex = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexOperation(DatabaseSetup.MessagesViewIndexName));

        Assert.That(freeTextIndex, Is.Null);
        Assert.That(nonFreeTextIndex, Is.Not.Null);
    }

    [Test]
    public async Task Indexes_should_be_reset_on_setup()
    {
        var index = new MessagesViewIndexWithFullTextSearch { Configuration = { ["Indexing.Static.SearchEngineType"] = SearchEngineType.Lucene.ToString() } };

        var indexWithCustomConfigStats = await UpdateIndex(index);

        Assert.That(indexWithCustomConfigStats.SearchEngineType, Is.EqualTo(SearchEngineType.Lucene));

        await DatabaseSetup.CreateIndexes(configuration.DocumentStore, true, TestTimeoutCancellationToken);

        await WaitForIndexDefinitionUpdate(indexWithCustomConfigStats);

        var indexAfterResetStats = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName));

        Assert.That(indexAfterResetStats.SearchEngineType, Is.EqualTo(SearchEngineType.Corax));
    }

    [Test]
    public async Task Indexes_should_not_be_reset_on_setup_when_locked_as_ignore()
    {
        var index = new MessagesViewIndexWithFullTextSearch
        {
            Configuration = { ["Indexing.Static.SearchEngineType"] = SearchEngineType.Lucene.ToString() },
            LockMode = IndexLockMode.LockedIgnore
        };

        var indexStatsBefore = await UpdateIndex(index);

        Assert.That(indexStatsBefore.SearchEngineType, Is.EqualTo(SearchEngineType.Lucene));

        await DatabaseSetup.CreateIndexes(configuration.DocumentStore, true, TestTimeoutCancellationToken);

        // raven will ignore the update since index was locked, so best we can do is wait a bit and check that settings hasn't changed
        await Task.Delay(1000);

        var indexStatsAfter = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName));
        Assert.That(indexStatsAfter.SearchEngineType, Is.EqualTo(SearchEngineType.Lucene));
    }

    [Test]
    public async Task Indexes_should_not_be_reset_on_setup_when_locked_as_error()
    {
        var index = new MessagesViewIndexWithFullTextSearch
        {
            Configuration = { ["Indexing.Static.SearchEngineType"] = SearchEngineType.Lucene.ToString() },
            LockMode = IndexLockMode.LockedError
        };

        await UpdateIndex(index);

        Assert.ThrowsAsync<IndexCreationException>(async () => await DatabaseSetup.CreateIndexes(configuration.DocumentStore, true, TestTimeoutCancellationToken));
    }

    async Task<IndexStats> UpdateIndex(IAbstractIndexCreationTask index)
    {
        var statsBefore = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName), TestTimeoutCancellationToken);

        await IndexCreation.CreateIndexesAsync([index], configuration.DocumentStore, null, null, TestTimeoutCancellationToken);

        return await WaitForIndexDefinitionUpdate(statsBefore);
    }

    async Task<IndexStats> WaitForIndexDefinitionUpdate(IndexStats oldStats)
    {
        while (true)
        {
            try
            {
                var newStats = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(oldStats.Name), TestTimeoutCancellationToken);

                if (newStats.CreatedTimestamp > oldStats.CreatedTimestamp)
                {
                    return newStats;
                }
            }
            catch (OperationCanceledException)
            {
                // keep going since we can get this if we query right when the update happens
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestTimeoutCancellationToken);
        }
    }
}