namespace ServiceControl.Persistence.Tests.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Indexes;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Persistence.RavenDB;
    using ServiceControl.RavenDB;

    [TestFixture]
    class IndexSetupTests : RavenPersistenceTestBase
    {
        [Test]
        public async Task Search_engine_configured_on_the_index_should_be_preserved_on_setup()
        {
            var index = new CustomChecksIndex { Conventions = DocumentStore.Conventions };
            var definition = index.CreateIndexDefinition();
            definition.Name = index.IndexName;
            definition.Configuration[IndexDeployment.StaticSearchEngineTypeKey] = SearchEngineType.Corax.ToString();

            var statsBefore = await DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName));
            await DocumentStore.Maintenance.SendAsync(new PutIndexesOperation(definition));
            var customizedStats = await WaitForIndexDefinitionUpdate(statsBefore);

            try
            {
                Assert.That(customizedStats.SearchEngineType, Is.EqualTo(SearchEngineType.Corax));

                await IndexDeployment.CreateIndexesAsync(typeof(DatabaseSetup).Assembly, DocumentStore);

                var replacement = await DocumentStore.Maintenance.SendAsync(new GetIndexOperation(Constants.Documents.Indexing.SideBySideIndexNamePrefix + index.IndexName));
                var statsAfter = await DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName));

                Assert.That(replacement, Is.Null, "Setup should not trigger a rebuild of an index whose only difference is the configured search engine");
                Assert.That(statsAfter.CreatedTimestamp, Is.EqualTo(customizedStats.CreatedTimestamp));
                Assert.That(statsAfter.SearchEngineType, Is.EqualTo(SearchEngineType.Corax));
            }
            finally
            {
                // Tests share the database. Restore the index to the state that setup creates.
                await IndexCreation.CreateIndexesAsync([new CustomChecksIndex { Configuration = { [IndexDeployment.StaticSearchEngineTypeKey] = SearchEngineType.Lucene.ToString() } }], DocumentStore);
                await WaitForIndexDefinitionUpdate(customizedStats);
            }
        }

        [TestCase(IndexLockMode.LockedIgnore)]
        [TestCase(IndexLockMode.LockedError)]
        public async Task Setup_should_fail_when_a_locked_index_has_a_changed_definition(IndexLockMode lockMode)
        {
            var index = new CustomChecksIndex { Conventions = DocumentStore.Conventions };
            var definition = index.CreateIndexDefinition();
            definition.Name = index.IndexName;
            definition.LockMode = lockMode;
            definition.Fields[nameof(CustomCheck.Category)] = new IndexFieldOptions { Storage = FieldStorage.Yes };

            var statsBefore = await DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName));
            await DocumentStore.Maintenance.SendAsync(new PutIndexesOperation(definition));
            var lockedStats = await WaitForIndexDefinitionUpdate(statsBefore);

            try
            {
                var exception = Assert.ThrowsAsync<Exception>(async () => await IndexDeployment.CreateIndexesAsync(typeof(DatabaseSetup).Assembly, DocumentStore));

                var definitionAfter = await DocumentStore.Maintenance.SendAsync(new GetIndexOperation(index.IndexName));

                Assert.That(exception.Message, Does.Contain(index.IndexName));
                Assert.That(definitionAfter.LockMode, Is.EqualTo(lockMode));
                Assert.That(definitionAfter.Fields, Does.ContainKey(nameof(CustomCheck.Category)));
            }
            finally
            {
                // Tests share the database. Restore the index to the state that setup creates.
                await DocumentStore.Maintenance.SendAsync(new SetIndexesLockOperation(index.IndexName, IndexLockMode.Unlock));
                await IndexCreation.CreateIndexesAsync([new CustomChecksIndex { Configuration = { [IndexDeployment.StaticSearchEngineTypeKey] = SearchEngineType.Lucene.ToString() } }], DocumentStore);
                await WaitForIndexDefinitionUpdate(lockedStats);
            }
        }

        async Task<IndexStats> WaitForIndexDefinitionUpdate(IndexStats oldStats)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            while (true)
            {
                var newStats = await DocumentStore.Maintenance.SendAsync(new GetIndexStatisticsOperation(oldStats.Name), timeout.Token);

                if (newStats.CreatedTimestamp > oldStats.CreatedTimestamp)
                {
                    return newStats;
                }

                await Task.Delay(100, timeout.Token);
            }
        }
    }
}
