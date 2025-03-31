namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Auditing;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    [TestFixture]
    class AuditTests : PersistenceTestFixture
    {
        public override Task Setup()
        {
            SetSettings = s =>
            {
                s.MaxBodySizeToStore = MAX_BODY_SIZE;
            };
            return base.Setup();
        }

        [Test]
        public async Task Basic_Roundtrip()
        {
            var message = MakeMessage("MyMessageId");

            await IngestProcessedMessagesAudits(
                message
                );

            var queryResult = await DataStore.QueryMessages("MyMessageId", new PagingInfo(), new SortInfo("Id", "asc"), null, TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.Results, Has.Count.EqualTo(1));
            Assert.That(queryResult.Results[0].MessageId, Is.EqualTo("MyMessageId"));
        }

        [Test]
        public async Task Handles_no_results_gracefully()
        {
            var nonExistingMessage = Guid.NewGuid().ToString();
            var queryResult = await DataStore.QueryMessages(nonExistingMessage, new PagingInfo(), new SortInfo("Id", "asc"), null, TestContext.CurrentContext.CancellationToken);

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
            );

            var queryResult = await DataStore.QueryMessagesByConversationId(conversationId, new PagingInfo(),
                new SortInfo("message_id", "asc"), TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.Results, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Can_query_by_message_type()
        {
            await IngestProcessedMessagesAudits(
                MakeMessage(messageType: "MyMessageType"),
                MakeMessage(messageType: "OtherMessageType"),
                MakeMessage(messageType: "MyMessageType")
            );

            var queryResult = await DataStore.QueryMessages("MyMessageType", new PagingInfo(),
                new SortInfo("message_id", "asc"), null, TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.Results, Has.Count.EqualTo(2));
        }
        [Test]
        public async Task Can_roundtrip_message_body()
        {
            string expectedContentType = "text/xml";
            var unitOfWork = await StartAuditUnitOfWork(1);

            var body = new byte[100];
            Random.Shared.NextBytes(body);
            var processedMessage = MakeMessage();

            await unitOfWork.RecordProcessedMessage(processedMessage, body);

            await unitOfWork.DisposeAsync();

            var bodyId = GetBodyId(processedMessage);

            var retrievedMessage = await DataStore.GetMessageBody(bodyId, TestContext.CurrentContext.CancellationToken);

            Assert.That(retrievedMessage, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(retrievedMessage.Found, Is.True);
                Assert.That(retrievedMessage.HasContent, Is.True);
                Assert.That(retrievedMessage.ContentLength, Is.EqualTo(body.Length));
                Assert.That(retrievedMessage.ETag, Is.Not.Null.Or.Empty);
                Assert.That(retrievedMessage.StreamContent, Is.Not.Null);
                Assert.That(retrievedMessage.ContentType, Is.EqualTo(expectedContentType));
            });

            var resultBody = new byte[body.Length];
            var readBytes = await retrievedMessage.StreamContent.ReadAsync(resultBody, 0, body.Length);
            Assert.Multiple(() =>
            {
                Assert.That(readBytes, Is.EqualTo(body.Length));
                Assert.That(resultBody, Is.EqualTo(body));
            });
        }

        [Test]
        public async Task Does_respect_max_message_body()
        {
            var unitOfWork = await StartAuditUnitOfWork(1);

            var body = new byte[MAX_BODY_SIZE + 1000];
            Random.Shared.NextBytes(body);
            var processedMessage = MakeMessage();

            await unitOfWork.RecordProcessedMessage(processedMessage, body);

            await unitOfWork.DisposeAsync();

            var bodyId = GetBodyId(processedMessage);

            var retrievedMessage = await DataStore.GetMessageBody(bodyId, TestContext.CurrentContext.CancellationToken);

            Assert.That(retrievedMessage, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(retrievedMessage.Found, Is.True);
                Assert.That(retrievedMessage.HasContent, Is.False);
            });

        }

        [Test]
        public async Task Deduplicates_messages_in_same_batch()
        {
            var unitOfWork = await StartAuditUnitOfWork(1);
            var messageId = "duplicatedId";
            var processingEndpoint = "endpoint";
            var processingStarted = DateTime.UtcNow;

            var processedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: processingStarted);
            var duplicatedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: processingStarted);
            await unitOfWork.RecordProcessedMessage(processedMessage);
            await unitOfWork.RecordProcessedMessage(duplicatedMessage);

            await unitOfWork.DisposeAsync();

            await configuration.CompleteDBOperation();

            var queryResult = await DataStore.GetMessages(false, new PagingInfo(), new SortInfo("message_id", "asc"), null, TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.QueryStats.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Deduplicates_messages_in_different_batches()
        {
            var messageId = "duplicatedId";
            var processingEndpoint = "endpoint";
            var processingStarted = DateTime.UtcNow;

            var processedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: processingStarted);
            var unitOfWork1 = await StartAuditUnitOfWork(1);
            await unitOfWork1.RecordProcessedMessage(processedMessage);
            await unitOfWork1.DisposeAsync();

            var duplicatedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: processingStarted);
            var unitOfWork2 = await StartAuditUnitOfWork(1);
            await unitOfWork2.RecordProcessedMessage(duplicatedMessage);
            await unitOfWork2.DisposeAsync();

            await configuration.CompleteDBOperation();

            var queryResult = await DataStore.GetMessages(false, new PagingInfo(), new SortInfo("message_id", "asc"), null, TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.QueryStats.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Does_not_deduplicate_with_different_processing_started_header()
        {
            var unitOfWork = await StartAuditUnitOfWork(1);
            var messageId = "duplicatedId";
            var processingEndpoint = "endpoint";
            var processingStarted = DateTime.UtcNow;
            var duplicatedProcessingStarted = processingStarted.AddSeconds(5);

            var processedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: processingStarted);
            var duplicatedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: duplicatedProcessingStarted);
            await unitOfWork.RecordProcessedMessage(processedMessage);
            await unitOfWork.RecordProcessedMessage(duplicatedMessage);

            await unitOfWork.DisposeAsync();

            await configuration.CompleteDBOperation();

            var queryResult = await DataStore.GetMessages(false, new PagingInfo(), new SortInfo("message_id", "asc"), null, TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.QueryStats.TotalCount, Is.EqualTo(2));
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
            MessageIntent intent = MessageIntent.Send,
            string conversationId = null,
            string processingEndpoint = null,
            DateTime? processingStarted = null,
            string messageType = null
        )
        {
            messageId ??= Guid.NewGuid().ToString();
            conversationId ??= Guid.NewGuid().ToString();
            processingEndpoint ??= "SomeEndpoint";
            messageType ??= "MyMessageType";

            var metadata = new Dictionary<string, object>
            {
                { "MessageId", messageId },
                { "MessageIntent", intent },
                { "CriticalTime", TimeSpan.FromSeconds(5) },
                { "ProcessingTime", TimeSpan.FromSeconds(1) },
                { "DeliveryTime", TimeSpan.FromSeconds(4) },
                { "IsSystemMessage", false },
                { "MessageType", messageType },
                { "IsRetried", false },
                { "ConversationId", conversationId },
                //{ "ContentLength", 10}
            };

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, messageId },
                { Headers.ProcessingEndpoint, processingEndpoint },
                { Headers.MessageIntent, intent.ToString() },
                { Headers.ConversationId, conversationId },
                { Headers.ProcessingStarted, DateTimeOffsetHelper.ToWireFormattedString(processingStarted ?? DateTime.UtcNow) },
                { Headers.EnclosedMessageTypes, messageType }
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

        const int MAX_BODY_SIZE = 20536;
    }
}