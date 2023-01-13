﻿namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Auditing;
    using Monitoring;
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
                s.AuditRetentionPeriod = TimeSpan.FromSeconds(2);
                s.PersisterSpecificSettings["ExpirationProcessTimerInSeconds"] = 3.ToString();
            };
            return base.Setup();
        }

        [Test]
        public async Task AuditMessageRetention()
        {
            var message = MakeMessage("MyMessageId");

            await IngestProcessedMessagesAudits(
                message
                ).ConfigureAwait(false);

            var queryResultBeforeExpiration = await DataStore.QueryMessages("MyMessageId", new PagingInfo(), new SortInfo("Id", "asc"))
                .ConfigureAwait(false);

            await Task.Delay(4000).ConfigureAwait(false);

            var queryResultAfterExpiration = await DataStore.QueryMessages("MyMessageId", new PagingInfo(), new SortInfo("Id", "asc"))
                .ConfigureAwait(false);

            Assert.That(queryResultBeforeExpiration.Results.Count, Is.EqualTo(1));
            Assert.That(queryResultBeforeExpiration.Results[0].MessageId, Is.EqualTo("MyMessageId"));

            Assert.That(queryResultAfterExpiration.Results.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task KnownEndpointRetention()
        {
            var knownEndpoint = new KnownEndpoint()
            {
                Host = "Myself",
                HostId = Guid.NewGuid(),
                Id = "KnownEndpoints/1234123",
                LastSeen = DateTime.UtcNow,
                Name = "Wazowsky"
            };

            await IngestKnownEndpoints(
                knownEndpoint
            ).ConfigureAwait(false);

            var queryResultBeforeExpiration = await DataStore.QueryKnownEndpoints()
                .ConfigureAwait(false);

            await Task.Delay(4000).ConfigureAwait(false);

            var queryResultAfterExpiration = await DataStore.QueryKnownEndpoints()
                .ConfigureAwait(false);

            Assert.That(queryResultBeforeExpiration.Results.Count, Is.EqualTo(1));
            Assert.That(queryResultBeforeExpiration.Results[0].EndpointDetails.Name, Is.EqualTo("Wazowsky"));

            Assert.That(queryResultAfterExpiration.Results.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task SagaSnapshotRetention()
        {
            var sagaId = Guid.NewGuid();
            var otherSagaId = Guid.NewGuid();

            await IngestSagaAudits(
                new SagaSnapshot { SagaId = sagaId },
                new SagaSnapshot { SagaId = otherSagaId },
                new SagaSnapshot { SagaId = sagaId }
            );

            var queryResultBeforeExpiration = await DataStore.QuerySagaHistoryById(sagaId);

            await Task.Delay(4000).ConfigureAwait(false);

            var queryResultAfterExpiration = await DataStore.QuerySagaHistoryById(sagaId);

            Assert.That(queryResultBeforeExpiration.Results, Is.Not.Null);
            Assert.That(queryResultBeforeExpiration.Results.Changes.Count, Is.EqualTo(2));
            Assert.That(queryResultAfterExpiration.Results, Is.Null);
        }

        ProcessedMessage MakeMessage(
            string messageId = null,
            MessageIntentEnum intent = MessageIntentEnum.Send,
            string conversationId = null,
            string processingEndpoint = null
        )
        {
            messageId = messageId ?? Guid.NewGuid().ToString();
            conversationId = conversationId ?? Guid.NewGuid().ToString();
            processingEndpoint = processingEndpoint ?? "SomeEndpoint";

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
            var unitOfWork = StartAuditUnitOfWork(processedMessages.Length);
            foreach (var processedMessage in processedMessages)
            {
                await unitOfWork.RecordProcessedMessage(processedMessage)
                    .ConfigureAwait(false);
            }
            await unitOfWork.DisposeAsync().ConfigureAwait(false);
            await configuration.CompleteDBOperation().ConfigureAwait(false);
        }

        async Task IngestKnownEndpoints(params KnownEndpoint[] knownEndpoints)
        {
            var unitOfWork = StartAuditUnitOfWork(knownEndpoints.Length);
            foreach (var knownEndpoint in knownEndpoints)
            {
                await unitOfWork.RecordKnownEndpoint(knownEndpoint)
                    .ConfigureAwait(false);
            }
            await unitOfWork.DisposeAsync().ConfigureAwait(false);
            await configuration.CompleteDBOperation().ConfigureAwait(false);
        }

        async Task IngestSagaAudits(params SagaSnapshot[] snapshots)
        {
            var unitOfWork = StartAuditUnitOfWork(snapshots.Length);
            foreach (var snapshot in snapshots)
            {
                await unitOfWork.RecordSagaSnapshot(snapshot).ConfigureAwait(false);
            }
            await unitOfWork.DisposeAsync().ConfigureAwait(false);
            await configuration.CompleteDBOperation();
        }
    }
}