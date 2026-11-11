namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Auditing;
using NUnit.Framework;
using Persistence.RavenDB.Indexes;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Documents.Session;
using ServiceControl.RavenDB;
using ServiceControl.SagaAudit;

[TestFixture]
class IndexDeploymentTests : CleanDatabaseFixture
{
    const string BodyText = "The zebrafish swims upstream";
    const string BodySearchTerm = "zebrafish";
    const string AnalyzerTypeName = "Lucene.Net.Analysis.Standard.StandardAnalyzer, Lucene.Net, Version=3.0.3.0, Culture=neutral, PublicKeyToken=85089178b9ac3181";

    [TestCase(typeof(MessagesViewIndexWithFullTextSearch))]
    [TestCase(typeof(MessagesViewIndex))]
    [TestCase(typeof(SagaDetailsIndex))]
    [TestCase(typeof(FailedAuditImportIndex))]
    public async Task Index_should_deploy_to_a_clean_database_and_be_queryable(Type indexType)
    {
        var index = (AbstractIndexCreationTask)Activator.CreateInstance(indexType);

        await IndexDeployment.CreateIndexesAsync([index], Store, TestTimeoutCancellationToken);

        var definition = await Store.Maintenance.SendAsync(new GetIndexOperation(index.IndexName), TestTimeoutCancellationToken);
        Assert.That(definition, Is.Not.Null);

        if (index is MessagesViewIndexWithFullTextSearch)
        {
            await AssertMessagesAreSearchable(index.IndexName, bodyIsSearchable: true);
        }
        else if (index is MessagesViewIndex)
        {
            await AssertMessagesAreSearchable(index.IndexName, bodyIsSearchable: false);
        }
        else if (index is SagaDetailsIndex)
        {
            await AssertSagaHistoryIsReduced();
        }
        else
        {
            await AssertFailedAuditImportIsIndexed();
        }

        var stats = await Store.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName), TestTimeoutCancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stats.State, Is.EqualTo(IndexState.Normal));
            Assert.That(stats.ErrorsCount, Is.Zero);
            Assert.That(stats.SearchEngineType, Is.EqualTo(SearchEngineType.Lucene));
        }
    }

    [TestCase(typeof(MessagesViewIndexWithFullTextSearch))]
    [TestCase(typeof(MessagesViewIndex))]
    public async Task Full_text_indexes_should_use_the_lucene_standard_analyzer_on_the_query_field(Type indexType)
    {
        var index = (AbstractIndexCreationTask)Activator.CreateInstance(indexType);

        await IndexDeployment.CreateIndexesAsync([index], Store, TestTimeoutCancellationToken);

        var definition = await Store.Maintenance.SendAsync(new GetIndexOperation(index.IndexName), TestTimeoutCancellationToken);

        Assert.That(definition.Fields[nameof(MessagesViewIndex.SortAndFilterOptions.Query)].Analyzer, Is.EqualTo(AnalyzerTypeName));
    }

    async Task AssertMessagesAreSearchable(string indexName, bool bodyIsSearchable)
    {
        var message = AuditTestData.CreateProcessedMessage(messageId: "MyMessageId");
        message.Id = $"ProcessedMessages-{DateTime.UtcNow.Ticks}-{message.GetProcessingId()}";

        using (var session = Store.OpenAsyncSession())
        {
            await session.StoreAsync(message, message.Id, TestTimeoutCancellationToken);
            session.Advanced.Attachments.Store(message.Id, "body", new MemoryStream(Encoding.UTF8.GetBytes(BodyText)), "text/plain");
            await session.SaveChangesAsync(TestTimeoutCancellationToken);
        }

        await Store.WaitForIndexingAsync(TestTimeoutCancellationToken);

        using (var session = Store.OpenAsyncSession())
        {
            var byMetadata = await Search(session, indexName, "MyMessageId");
            var byBody = await Search(session, indexName, BodySearchTerm);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(byMetadata, Has.Count.EqualTo(1), "A search over the message metadata should find the message");
                Assert.That(byBody, Has.Count.EqualTo(bodyIsSearchable ? 1 : 0), "A search over the stored body should find the message only with the full-text index");
            }
        }

        static Task<List<MessagesViewIndex.SortAndFilterOptions>> Search(IAsyncDocumentSession session, string indexName, string searchTerm) =>
            session.Query<MessagesViewIndex.SortAndFilterOptions>(indexName)
                .Search(x => x.Query, searchTerm)
                .ToListAsync();
    }

    async Task AssertSagaHistoryIsReduced()
    {
        var sagaId = Guid.NewGuid();

        using (var session = Store.OpenAsyncSession())
        {
            await session.StoreAsync(new SagaSnapshot { SagaId = sagaId, SagaType = "MySagaType", FinishTime = DateTime.UtcNow }, TestTimeoutCancellationToken);
            await session.StoreAsync(new SagaSnapshot { SagaId = sagaId, SagaType = "MySagaType", FinishTime = DateTime.UtcNow.AddSeconds(1) }, TestTimeoutCancellationToken);
            await session.SaveChangesAsync(TestTimeoutCancellationToken);
        }

        await Store.WaitForIndexingAsync(TestTimeoutCancellationToken);

        using (var session = Store.OpenAsyncSession())
        {
            var history = await session.Query<SagaHistory, SagaDetailsIndex>().SingleOrDefaultAsync(x => x.SagaId == sagaId, TestTimeoutCancellationToken);

            Assert.That(history, Is.Not.Null);
            Assert.That(history.Changes, Has.Count.EqualTo(2));
        }
    }

    async Task AssertFailedAuditImportIsIndexed()
    {
        using (var session = Store.OpenAsyncSession())
        {
            await session.StoreAsync(new FailedAuditImport(), "FailedAuditImports/1", TestTimeoutCancellationToken);
            await session.SaveChangesAsync(TestTimeoutCancellationToken);
        }

        await Store.WaitForIndexingAsync(TestTimeoutCancellationToken);

        using (var session = Store.OpenAsyncSession())
        {
            var count = await session.Query<FailedAuditImport, FailedAuditImportIndex>().CountAsync(TestTimeoutCancellationToken);

            Assert.That(count, Is.EqualTo(1));
        }
    }
}
