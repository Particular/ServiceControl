﻿namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Auditing;
    using Infrastructure;
    using NServiceBus;
    using NUnit.Framework;

    [TestFixture]
    class AuditTests : PersistenceTestFixture
    {
        [Test]
        public async Task Basic_Roundtrip()
        {
            var message = MakeMessage("MyMessageId");

            await IngestProcessedMessagesAudits(
                message
                ).ConfigureAwait(false);

            var queryResult = await DataStore.QueryMessages("MyMessageId", new PagingInfo(), new SortInfo("Id", "asc"))
                .ConfigureAwait(false);

            Assert.That(queryResult.Results.Count, Is.EqualTo(1));
            Assert.That(queryResult.Results[0].MessageId, Is.EqualTo("MyMessageId"));
        }

        [Test]
        public async Task Handles_no_results_gracefully()
        {
            var nonExistingMessage = Guid.NewGuid().ToString();
            var queryResult = await DataStore.QueryMessages(nonExistingMessage, new PagingInfo(), new SortInfo("Id", "asc")).ConfigureAwait(false);

            Assert.That(queryResult.Results, Is.Empty);
        }

        [Test]
        public async Task Can_query_by_conversation_id()
        {
            var conversationId = Guid.NewGuid().ToString();
            var otherConversationId = Guid.NewGuid().ToString();

            await IngestProcessedMessagesAudits(
                MakeMessage(conversationId: conversationId),
                MakeMessage(conversationId: otherConversationId),
                MakeMessage(conversationId: conversationId)
            ).ConfigureAwait(false);

            var queryResult = await DataStore.QueryMessagesByConversationId(conversationId, new PagingInfo(),
                new SortInfo("MessageId", "asc")).ConfigureAwait(false);

            Assert.That(queryResult.Results.Count, Is.EqualTo(2));
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
                { "ContentLength", 25 },
                { "MessageType", "MyMessageType" },
                { "IsRetried", false },
                { "ConversationId", conversationId }
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
    }
}