namespace ServiceControl.Persistence.Tests.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Indexes;
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
                // The database is shared across tests, restore the index as setup creates it
                await IndexCreation.CreateIndexesAsync([new CustomChecksIndex { Configuration = { [IndexDeployment.StaticSearchEngineTypeKey] = SearchEngineType.Lucene.ToString() } }], DocumentStore);
                await WaitForIndexDefinitionUpdate(customizedStats);
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
