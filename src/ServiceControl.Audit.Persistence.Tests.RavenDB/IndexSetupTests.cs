namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Persistence.RavenDB;
using Persistence.RavenDB.Indexes;
using Raven.Client;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Documents.Indexes;
using ServiceControl.RavenDB;

[TestFixture]
class IndexSetupTests : PersistenceTestFixture
{
    [Test]
    public async Task Lucene_should_be_the_default_search_engine_type_for_new_databases()
    {
        var indexes = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexesOperation(0, int.MaxValue));

        foreach (var index in indexes)
        {
            var indexStats = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(DatabaseSetup.MessagesViewIndexWithFulltextSearchName));
            Assert.That(indexStats.SearchEngineType, Is.EqualTo(SearchEngineType.Lucene), $"{index.Name} is not using Lucene");
        }
    }

    [Test]
    public async Task Startup_check_should_not_report_corax_indexes_for_new_database()
    {
        var coraxIndexes = await StartupChecks.FindIndexesUsingCorax(configuration.DocumentStore, configuration.DocumentStore.Database, TestTimeoutCancellationToken);

        Assert.That(coraxIndexes, Is.Empty);
    }

    [Test]
    public async Task Startup_check_should_report_indexes_using_corax()
    {
        var index = new MessagesViewIndexWithFullTextSearch { Configuration = { [IndexDeployment.StaticSearchEngineTypeKey] = SearchEngineType.Corax.ToString() } };

        await UpdateIndex(index);

        var coraxIndexes = await StartupChecks.FindIndexesUsingCorax(configuration.DocumentStore, configuration.DocumentStore.Database, TestTimeoutCancellationToken);

        Assert.That(coraxIndexes, Is.EqualTo(new[] { index.IndexName }));
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
    public async Task New_indexes_should_be_created_with_lucene()
    {
        var index = new FailedAuditImportIndex();

        await configuration.DocumentStore.Maintenance.SendAsync(new DeleteIndexOperation(index.IndexName), TestTimeoutCancellationToken);

        await DatabaseSetup.CreateIndexes(configuration.DocumentStore, true, TestTimeoutCancellationToken);

        var definition = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexOperation(index.IndexName), TestTimeoutCancellationToken);

        // The search engine is set on the index, not inherited. It then also applies to databases that default to Corax.
        Assert.That(definition.Configuration, Does.ContainKey(IndexDeployment.StaticSearchEngineTypeKey).WithValue(SearchEngineType.Lucene.ToString()));
    }

    [TestCase(true, TestName = "Search engine set in the index definition through SearchEngineType should take precedence over the one on the server")]
    [TestCase(false, TestName = "Search engine set in the index definition through Configuration should take precedence over the one on the server")]
    public async Task Search_engine_set_in_the_index_definition_should_take_precedence_over_the_one_on_the_server(bool useSearchEngineTypeProperty)
    {
        var statsBefore = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(nameof(FailedAuditImportIndex)), TestTimeoutCancellationToken);

        Assert.That(statsBefore.SearchEngineType, Is.EqualTo(SearchEngineType.Lucene));

        await IndexDeployment.CreateIndexesAsync([new FailedAuditImportIndexPinnedToCorax(useSearchEngineTypeProperty)], configuration.DocumentStore, TestTimeoutCancellationToken);

        var statsAfter = await WaitForIndexDefinitionUpdate(statsBefore);

        Assert.That(statsAfter.SearchEngineType, Is.EqualTo(SearchEngineType.Corax));
    }

    [Test]
    public async Task Search_engine_configured_on_the_index_should_be_preserved_on_setup()
    {
        var index = new MessagesViewIndexWithFullTextSearch { Configuration = { [IndexDeployment.StaticSearchEngineTypeKey] = SearchEngineType.Corax.ToString() } };

        var indexStatsBefore = await UpdateIndex(index);

        Assert.That(indexStatsBefore.SearchEngineType, Is.EqualTo(SearchEngineType.Corax));

        await DatabaseSetup.CreateIndexes(configuration.DocumentStore, true, TestTimeoutCancellationToken);

        var replacement = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexOperation(Constants.Documents.Indexing.SideBySideIndexNamePrefix + index.IndexName), TestTimeoutCancellationToken);
        var indexStatsAfter = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName), TestTimeoutCancellationToken);

        Assert.That(replacement, Is.Null, "Setup should not trigger a rebuild of an index whose only difference is the configured search engine");
        Assert.That(indexStatsAfter.CreatedTimestamp, Is.EqualTo(indexStatsBefore.CreatedTimestamp));
        Assert.That(indexStatsAfter.SearchEngineType, Is.EqualTo(SearchEngineType.Corax));
    }

    [Test]
    public async Task Indexes_should_be_reset_on_setup_keeping_the_configured_search_engine()
    {
        var customizedStats = await PutCustomizedIndex(SearchEngineType.Corax);

        Assert.That(customizedStats.SearchEngineType, Is.EqualTo(SearchEngineType.Corax));

        await DatabaseSetup.CreateIndexes(configuration.DocumentStore, true, TestTimeoutCancellationToken);

        var resetStats = await WaitForIndexDefinitionUpdate(customizedStats);
        var resetDefinition = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexOperation(customizedStats.Name), TestTimeoutCancellationToken);

        Assert.That(resetDefinition.Fields, Does.Not.ContainKey(CustomizedField), "Customizations made to the index definition should be reset");
        Assert.That(resetStats.SearchEngineType, Is.EqualTo(SearchEngineType.Corax), "The search engine configured on the index should be kept when the index is rebuilt");
    }

    [Test]
    public async Task Pending_replacement_using_the_database_default_should_be_discarded_in_favor_of_the_configured_search_engine()
    {
        var index = new MessagesViewIndexWithFullTextSearch { Configuration = { [IndexDeployment.StaticSearchEngineTypeKey] = SearchEngineType.Corax.ToString() } };
        var replacementName = Constants.Documents.Indexing.SideBySideIndexNamePrefix + index.IndexName;

        var originalStats = await UpdateIndex(index);

        // Stop indexing so that the replacement cannot catch up and swap. A large database under load shows the same behavior.
        await configuration.DocumentStore.Maintenance.SendAsync(new StopIndexingOperation(), TestTimeoutCancellationToken);

        try
        {
            // Versions before the fix deployed the definition without the configured search engine.
            // This creates a replacement that uses the database default.
            await IndexCreation.CreateIndexesAsync([new MessagesViewIndexWithFullTextSearch()], configuration.DocumentStore, null, null, TestTimeoutCancellationToken);

            var defaultReplacement = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(replacementName), TestTimeoutCancellationToken);
            Assert.That(defaultReplacement.SearchEngineType, Is.EqualTo(SearchEngineType.Lucene));

            await DatabaseSetup.CreateIndexes(configuration.DocumentStore, true, TestTimeoutCancellationToken);

            var replacementAfterSetup = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexOperation(replacementName), TestTimeoutCancellationToken);
            var originalAfterSetup = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName), TestTimeoutCancellationToken);

            Assert.That(replacementAfterSetup, Is.Null, "The definition matches the existing index again, so the pending replacement should be discarded");
            Assert.That(originalAfterSetup.CreatedTimestamp, Is.EqualTo(originalStats.CreatedTimestamp));
            Assert.That(originalAfterSetup.SearchEngineType, Is.EqualTo(SearchEngineType.Corax));
        }
        finally
        {
            await configuration.DocumentStore.Maintenance.SendAsync(new StartIndexingOperation(), TestTimeoutCancellationToken);
        }
    }

    [Test]
    public async Task Indexes_should_not_be_reset_on_setup_when_locked_as_ignore()
    {
        var customizedStats = await PutCustomizedIndex(SearchEngineType.Corax, IndexLockMode.LockedIgnore);

        await DatabaseSetup.CreateIndexes(configuration.DocumentStore, true, TestTimeoutCancellationToken);

        // RavenDB ignores the update because the index is locked. Wait a moment and then make sure that the definition did not change.
        await Task.Delay(1000);

        var definitionAfter = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexOperation(customizedStats.Name), TestTimeoutCancellationToken);
        var indexStatsAfter = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(customizedStats.Name), TestTimeoutCancellationToken);

        Assert.That(definitionAfter.Fields, Does.ContainKey(CustomizedField));
        Assert.That(indexStatsAfter.SearchEngineType, Is.EqualTo(SearchEngineType.Corax));
    }

    [Test]
    public async Task Indexes_should_not_be_reset_on_setup_when_locked_as_error()
    {
        await PutCustomizedIndex(SearchEngineType.Corax, IndexLockMode.LockedError);

        Assert.ThrowsAsync<IndexCreationException>(async () => await DatabaseSetup.CreateIndexes(configuration.DocumentStore, true, TestTimeoutCancellationToken));
    }

    // Simulates an index changed outside ServiceControl, for example in RavenDB Studio. Its definition is different from ours.
    async Task<IndexStats> PutCustomizedIndex(SearchEngineType searchEngineType, IndexLockMode lockMode = IndexLockMode.Unlock)
    {
        var index = new MessagesViewIndexWithFullTextSearch { Conventions = configuration.DocumentStore.Conventions };
        var definition = index.CreateIndexDefinition();
        definition.Name = index.IndexName;
        definition.LockMode = lockMode;
        definition.Configuration[IndexDeployment.StaticSearchEngineTypeKey] = searchEngineType.ToString();
        definition.Fields[CustomizedField] = new IndexFieldOptions { Storage = FieldStorage.Yes };

        var statsBefore = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName), TestTimeoutCancellationToken);

        await configuration.DocumentStore.Maintenance.SendAsync(new PutIndexesOperation(definition), TestTimeoutCancellationToken);

        return await WaitForIndexDefinitionUpdate(statsBefore);
    }

    async Task<IndexStats> UpdateIndex(IAbstractIndexCreationTask index)
    {
        var statsBefore = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName), TestTimeoutCancellationToken);

        await IndexCreation.CreateIndexesAsync([index], configuration.DocumentStore, null, null, TestTimeoutCancellationToken);

        return await WaitForIndexDefinitionUpdate(statsBefore);
    }

    class FailedAuditImportIndexPinnedToCorax : FailedAuditImportIndex
    {
        public FailedAuditImportIndexPinnedToCorax(bool useSearchEngineTypeProperty)
        {
            if (useSearchEngineTypeProperty)
            {
                SearchEngineType = Raven.Client.Documents.Indexes.SearchEngineType.Corax;
            }
            else
            {
                Configuration[IndexDeployment.StaticSearchEngineTypeKey] = Raven.Client.Documents.Indexes.SearchEngineType.Corax.ToString();
            }
        }

        public override string IndexName => nameof(FailedAuditImportIndex);
    }

    const string CustomizedField = nameof(MessagesViewIndex.SortAndFilterOptions.MessageId);

    // How many consecutive RavenExceptions from the stats query below get tolerated before letting one propagate for real.
    // RavenDB can throw a variety of transient errors for that race (seen so far: OperationCanceledException
    // from the read transaction being cancelled, and ObjectDisposedException from the old engine's index persistence being torn down).
    // A genuinely broken index should still fail the test.
    const int MaxTransientRavenExceptionRetries = 2;

    async Task<IndexStats> WaitForIndexDefinitionUpdate(IndexStats oldStats)
    {
        var transientFailures = 0;

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
#pragma warning disable PS0020
            catch (OperationCanceledException)
            {
                // keep going since we can get this if we query right when the update happens
            }
            catch (RavenException) when (transientFailures < MaxTransientRavenExceptionRetries)
            {
                transientFailures++;
            }
#pragma warning restore PS0020

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestTimeoutCancellationToken);
        }
    }
}