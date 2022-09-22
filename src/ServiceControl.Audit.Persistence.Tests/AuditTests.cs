﻿namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
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
                new SortInfo("message_id", "asc")).ConfigureAwait(false);

            Assert.That(queryResult.Results.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task Can_roundtrip_message_body()
        {
            var unitOfWork = AuditIngestionUnitOfWorkFactory.StartNew(1);

            var body = new byte[100];
            new Random().NextBytes(body);
            var processedMessage = MakeMessage();

            await unitOfWork.RecordProcessedMessage(processedMessage, body).ConfigureAwait(false);

            await unitOfWork.DisposeAsync().ConfigureAwait(false);

            var bodyId = GetBodyId(processedMessage);

            var retrievedMessage = await DataStore.GetMessageBody(bodyId).ConfigureAwait(false);

            Assert.That(retrievedMessage, Is.Not.Null);
            Assert.That(retrievedMessage.Found, Is.True);
            Assert.That(retrievedMessage.HasContent, Is.True);
        }

        string GetBodyId(ProcessedMessage processedMessage)
        {
            if (processedMessage.MessageMetadata.TryGetValue("BodyUrl", out var bodyUrlObj)
                && bodyUrlObj is string bodyUrl)
            {
                var match = Regex.Match(bodyUrl, "^/messages/(.*)/body$");
                if (match.Success)
                {
                    return match.Result("$1");
                }

                throw new Exception($"Do not know how to parse body url: {bodyUrl}");
            }

            throw new Exception($"Could not retrieve body url");
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
    }
}