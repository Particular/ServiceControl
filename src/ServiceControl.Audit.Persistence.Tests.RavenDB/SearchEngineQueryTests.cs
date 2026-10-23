namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Auditing.MessagesView;
using NUnit.Framework;
using Persistence.RavenDB;
using Persistence.RavenDB.Indexes;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using ServiceControl.Audit.Infrastructure;
using ServiceControl.RavenDB;
using ServiceControl.SagaAudit;

// Databases created before ServiceControl pinned the engine have no search engine setting, and DatabaseSetup pins them to Corax.
// The Corax fixture reproduces that: the database and its indexes exist, pinned to Corax, before the persistence starts.
[TestFixture(SearchEngineType.Lucene)]
[TestFixture(SearchEngineType.Corax)]
class SearchEngineQueryTests(SearchEngineType searchEngine) : PersistenceTestFixture
{
    static readonly Guid SagaId = Guid.NewGuid();
    static readonly DateTime FirstTimeSent = DateTime.UtcNow.AddMinutes(-10);

    public override async Task Setup()
    {
        databaseName = Guid.NewGuid().ToString();

        if (searchEngine == SearchEngineType.Corax)
        {
            await CreateExistingDatabaseWithCoraxIndexes();
        }

        SetSettings = s => s.PersisterSpecificSettings[RavenPersistenceConfiguration.DatabaseNameKey] = databaseName;

        await base.Setup();

        await IngestAudits();
    }

    [Test]
    public async Task Database_and_indexes_should_use_the_search_engine_under_test()
    {
        var databaseRecord = await configuration.DocumentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName), TestTimeoutCancellationToken);
        var indexStats = await configuration.DocumentStore.Maintenance.SendAsync(new GetIndexesStatisticsOperation(), TestTimeoutCancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(databaseRecord.Settings["Indexing.Static.SearchEngineType"], Is.EqualTo(searchEngine.ToString()));
            Assert.That(indexStats, Is.Not.Empty);
            Assert.That(indexStats.Select(i => i.SearchEngineType), Has.All.EqualTo(searchEngine));
        }
    }

    [Test]
    public async Task Message_list_should_be_sorted_by_every_sort_option()
    {
        (string Sort, string Direction, string ExpectedOrder)[] sortOptions =
        [
            ("time_sent", "asc", "A,B,C"),
            ("time_sent", "desc", "C,B,A"),
            ("message_id", "asc", "A,B,C"),
            ("message_id", "desc", "C,B,A"),
            ("message_type", "asc", "A,B,C"),
            ("message_type", "desc", "C,B,A"),
            ("critical_time", "asc", "B,C,A"),
            ("critical_time", "desc", "A,C,B"),
            ("processing_time", "asc", "C,A,B"),
            ("processing_time", "desc", "B,A,C"),
            ("delivery_time", "asc", "A,B,C"),
            ("delivery_time", "desc", "C,B,A"),
            ("processed_at", "asc", "A,B,C"),
            ("processed_at", "desc", "C,B,A")
        ];

        using (Assert.EnterMultipleScope())
        {
            foreach (var (sort, direction, expectedOrder) in sortOptions)
            {
                var result = await MessagesViewStore.GetMessages(false, new PagingInfo(), new SortInfo(sort, direction), cancellationToken: TestTimeoutCancellationToken);

                Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(MessageIds(expectedOrder)), $"Sorted by {sort} {direction}");
            }
        }
    }

    [Test]
    public async Task Message_list_should_be_paged_after_sorting()
    {
        var result = await MessagesViewStore.GetMessages(false, new PagingInfo(page: 2, pageSize: 1), new SortInfo("time_sent", "asc"), cancellationToken: TestTimeoutCancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(MessageIds("B")));
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Full_text_search_should_find_messages()
    {
        (string SearchTerm, string ExpectedMessages, string Description)[] searches =
        [
            ("zebrafish", "A,C", "A word in the body"),
            ("ZEBRAFISH", "A,C", "Case is ignored"),
            ("zebra*", "A,C", "A trailing wildcard"),
            ("bravo", "B", "A word in the body of one message"),
            ("TypeB", "B", "A message type from the metadata"),
            ("EndpointB", "B", "An endpoint name from the headers"),
            ("no-such-term", "", "No match")
        ];

        using (Assert.EnterMultipleScope())
        {
            foreach (var (searchTerm, expectedMessages, description) in searches)
            {
                var result = await MessagesViewStore.QueryMessages(searchTerm, new PagingInfo(), new SortInfo("time_sent", "asc"), cancellationToken: TestTimeoutCancellationToken);

                Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(MessageIds(expectedMessages)), $"Search for '{searchTerm}': {description}");
            }
        }
    }

    [Test]
    public async Task Full_text_search_should_combine_with_the_receiving_endpoint()
    {
        var endpointA = await MessagesViewStore.QueryMessagesByReceivingEndpointAndKeyword("EndpointA", "zebrafish", new PagingInfo(), new SortInfo("time_sent", "desc"), cancellationToken: TestTimeoutCancellationToken);
        var endpointB = await MessagesViewStore.QueryMessagesByReceivingEndpointAndKeyword("EndpointB", "zebrafish", new PagingInfo(), new SortInfo("time_sent", "desc"), cancellationToken: TestTimeoutCancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpointA.Results.Select(m => m.MessageId), Is.EqualTo(MessageIds("C,A")));
            Assert.That(endpointB.Results, Is.Empty);
        }
    }

    [Test]
    public async Task Receiving_endpoint_view_should_list_the_messages_of_the_endpoint()
    {
        var result = await MessagesViewStore.QueryMessagesByReceivingEndpoint(false, "EndpointA", new PagingInfo(), new SortInfo("critical_time", "asc"), cancellationToken: TestTimeoutCancellationToken);

        Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(MessageIds("C,A")));
    }

    [Test]
    public async Task Conversation_view_should_list_the_messages_of_the_conversation()
    {
        var result = await MessagesViewStore.QueryMessagesByConversationId("conversation-1", new PagingInfo(), new SortInfo("time_sent", "desc"), TestTimeoutCancellationToken);

        Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(MessageIds("B,A")));
    }

    [Test]
    public async Task Audit_counts_should_count_the_processed_messages_of_the_endpoint()
    {
        var result = await MessagesViewStore.QueryAuditCounts("EndpointA", TestTimeoutCancellationToken);

        Assert.That(result.Results.Sum(c => c.Count), Is.EqualTo(2));
    }

    [Test]
    public async Task Saga_view_should_contain_the_state_changes_of_the_saga_newest_first()
    {
        var result = await SagaHistoryStore.QuerySagaHistoryById(SagaId, TestTimeoutCancellationToken);

        Assert.That(result.Results, Is.Not.Null);
        Assert.That(result.Results.Changes.Select(c => c.StateAfterChange), Is.EqualTo(new[] { "second", "first" }));
    }

    static string[] MessageIds(string messages) => messages.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(m => $"message-{m.ToLowerInvariant()}").ToArray();

    async Task CreateExistingDatabaseWithCoraxIndexes()
    {
        var embeddedDatabase = await SharedEmbeddedServer.GetInstance();
        using var store = await embeddedDatabase.Connect();

        await store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(databaseName)));

        AbstractIndexCreationTask[] indexes = [new FailedAuditImportIndex(), new SagaDetailsIndex(), new MessagesViewIndexWithFullTextSearch()];

        foreach (var index in indexes)
        {
            index.Configuration[IndexDeployment.StaticSearchEngineTypeKey] = nameof(SearchEngineType.Corax);
            await index.ExecuteAsync(store, store.Conventions, databaseName);
        }
    }

    async Task IngestAudits()
    {
        await using var unitOfWork = await StartAuditUnitOfWork(3);

        // Each message differs in every sort field, in a different order
        await RecordMessage("A", "conversation-1", "EndpointA", 0, criticalTime: 3, processingTime: 2, deliveryTime: 1, "alpha zebrafish");
        await RecordMessage("B", "conversation-1", "EndpointB", 1, criticalTime: 1, processingTime: 3, deliveryTime: 2, "bravo");
        await RecordMessage("C", "conversation-2", "EndpointA", 2, criticalTime: 2, processingTime: 1, deliveryTime: 3, "charlie zebrafish");

        await unitOfWork.RecordSagaSnapshot(new SagaSnapshot { SagaId = SagaId, SagaType = "MySagaType", Endpoint = "EndpointA", StateAfterChange = "first", Status = SagaStateChangeStatus.New, StartTime = FirstTimeSent, FinishTime = FirstTimeSent.AddSeconds(1) });
        await unitOfWork.RecordSagaSnapshot(new SagaSnapshot { SagaId = SagaId, SagaType = "MySagaType", Endpoint = "EndpointA", StateAfterChange = "second", Status = SagaStateChangeStatus.Updated, StartTime = FirstTimeSent.AddSeconds(2), FinishTime = FirstTimeSent.AddSeconds(3) });

        await unitOfWork.Complete();
        await configuration.CompleteDBOperation();

        Task RecordMessage(string name, string conversationId, string endpoint, int minutesAfterFirst, int criticalTime, int processingTime, int deliveryTime, string body)
        {
            var timeSent = FirstTimeSent.AddMinutes(minutesAfterFirst);
            var message = AuditTestData.CreateProcessedMessage(
                messageId: $"message-{name.ToLowerInvariant()}",
                messageType: $"Type{name}",
                conversationId: conversationId,
                receivingEndpoint: endpoint,
                timeSent: timeSent,
                criticalTime: TimeSpan.FromSeconds(criticalTime),
                processingTime: TimeSpan.FromSeconds(processingTime),
                deliveryTime: TimeSpan.FromSeconds(deliveryTime),
                processedAt: new DateTimeOffset(timeSent));

            return unitOfWork.RecordProcessedMessage(message, Encoding.UTF8.GetBytes(body));
        }
    }

    string databaseName;
}
