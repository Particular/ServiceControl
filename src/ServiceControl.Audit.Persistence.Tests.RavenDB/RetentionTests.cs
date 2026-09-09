namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Auditing.MessagesView;
    using NServiceBus;
    using NUnit.Framework;
    using SagaAudit;
    using ServiceControl.Audit.Infrastructure;

    [TestFixture]
    class RetentionTests : PersistenceTestFixture
    {
        public override Task Setup()
        {
            SetSettings = s =>
            {
                s.AuditRetentionPeriod = RetentionPeriod;
                s.PersisterSpecificSettings["ExpirationProcessTimerInSeconds"] = 1.ToString();
            };
            return base.Setup();
        }

        [Test]
        public async Task AuditMessageRetention()
        {
            await WaitForIndexesToBeBuilt();

            var message = MakeMessage("MyMessageId");

            await IngestProcessedMessagesAudits(message);

            var queryResultBeforeExpiration = await QueryMessages(TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResultBeforeExpiration.Results, Has.Count.EqualTo(1));
            Assert.That(queryResultBeforeExpiration.Results[0].MessageId, Is.EqualTo("MyMessageId"));

            var queryResultAfterExpiration = await WaitUntil(QueryMessages, result => result.Results.Count == 0);

            Assert.That(queryResultAfterExpiration.Results, Is.Empty);

            Task<QueryResult<IList<MessagesView>>> QueryMessages(CancellationToken cancellationToken) =>
                DataStore.QueryMessages("MyMessageId", new PagingInfo(), new SortInfo("Id", "asc"), cancellationToken: cancellationToken);
        }

        [Test]
        public async Task SagaSnapshotRetention()
        {
            await WaitForIndexesToBeBuilt();

            var sagaId = Guid.NewGuid();
            var otherSagaId = Guid.NewGuid();

            await IngestSagaAudits(
                new SagaSnapshot { SagaId = sagaId },
                new SagaSnapshot { SagaId = otherSagaId },
                new SagaSnapshot { SagaId = sagaId }
            );

            var queryResultBeforeExpiration = await QuerySagaHistory(TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResultBeforeExpiration.Results, Is.Not.Null);
            Assert.That(queryResultBeforeExpiration.Results.Changes, Has.Count.EqualTo(2));

            var queryResultAfterExpiration = await WaitUntil(QuerySagaHistory, result => result.Results == null);

            Assert.That(queryResultAfterExpiration.Results, Is.Null);

            Task<QueryResult<SagaHistory>> QuerySagaHistory(CancellationToken cancellationToken) =>
                DataStore.QuerySagaHistoryById(sagaId, cancellationToken);
        }

        // The retention window starts at ingestion, so building the indexes of the fresh database must
        // not happen inside it: on a busy runner that alone has taken longer than the window.
        Task WaitForIndexesToBeBuilt() => configuration.CompleteDBOperation();

        // Polls instead of sleeping for a fixed time, so the test takes as long as expiration actually
        // needs and still tolerates a slow expiration pass.
        static async Task<T> WaitUntil<T>(Func<CancellationToken, Task<T>> query, Func<T, bool> condition)
        {
            var cancellationToken = TestContext.CurrentContext.CancellationToken;
            var deadline = DateTime.UtcNow + RetentionPeriod + TimeSpan.FromSeconds(15);
            var result = await query(cancellationToken);

            while (!condition(result) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(250, cancellationToken);
                result = await query(cancellationToken);
            }

            return result;
        }

        static readonly TimeSpan RetentionPeriod = TimeSpan.FromSeconds(3);

        ProcessedMessage MakeMessage(
            string messageId = null,
            MessageIntent intent = MessageIntent.Send,
            string conversationId = null,
            string processingEndpoint = null
        )
        {
            messageId ??= Guid.NewGuid().ToString();
            conversationId ??= Guid.NewGuid().ToString();
            processingEndpoint ??= "SomeEndpoint";

            var metadata = new Dictionary<string, object>
            {
                { "MessageId", messageId },
                { "MessageIntent", intent },
                { "CriticalTime", TimeSpan.FromSeconds(5) },
                { "ProcessingTime", TimeSpan.FromSeconds(1) },
                { "DeliveryTime", TimeSpan.FromSeconds(4) },
                { "IsSystemMessage", false },
                { "MessageType", "MyMessageType" },
                { "IsRetried", false },
                { "ConversationId", conversationId },
                { "ContentLength", 10}
            };

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, messageId },
                { Headers.ProcessingEndpoint, processingEndpoint },
                { Headers.MessageIntent, intent.ToString() },
                { Headers.ConversationId, conversationId }
            };

            return new ProcessedMessage(headers, metadata);
        }

        async Task IngestProcessedMessagesAudits(params ProcessedMessage[] processedMessages)
        {
            var unitOfWork = await StartAuditUnitOfWork(processedMessages.Length);
            foreach (var processedMessage in processedMessages)
            {
                await unitOfWork.RecordProcessedMessage(processedMessage);
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }

        async Task IngestSagaAudits(params SagaSnapshot[] snapshots)
        {
            var unitOfWork = await StartAuditUnitOfWork(snapshots.Length);
            foreach (var snapshot in snapshots)
            {
                await unitOfWork.RecordSagaSnapshot(snapshot);
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }
    }
}