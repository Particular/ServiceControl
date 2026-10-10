namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Auditing;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    [TestFixture]
    partial class AuditTests : PersistenceTestFixture
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

            var queryResult = await MessagesViewStore.QueryMessages("MyMessageId", new PagingInfo(), new SortInfo("Id", "asc"), cancellationToken: TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.Results, Has.Count.EqualTo(1));
            Assert.That(queryResult.Results[0].MessageId, Is.EqualTo("MyMessageId"));
        }

        [Test]
        public async Task Handles_no_results_gracefully()
        {
            var nonExistingMessage = Guid.NewGuid().ToString();
            var queryResult = await MessagesViewStore.QueryMessages(nonExistingMessage, new PagingInfo(), new SortInfo("Id", "asc"), cancellationToken: TestContext.CurrentContext.CancellationToken);

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

            var queryResult = await MessagesViewStore.QueryMessagesByConversationId(conversationId, new PagingInfo(),
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

            var queryResult = await MessagesViewStore.QueryMessages("MyMessageType", new PagingInfo(),
                new SortInfo("message_id", "asc"), cancellationToken: TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.Results, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Message_body_validator_is_stable_across_reads()
        {
            await using var unitOfWork = await StartAuditUnitOfWork(1);

            var body = Encoding.UTF8.GetBytes("{\"Text\":\"The body\"}");
            var processedMessage = MakeMessage();

            await unitOfWork.RecordProcessedMessage(processedMessage, body);

            await unitOfWork.Complete();

            var bodyId = GetBodyId(processedMessage);

            var first = await MessagesViewStore.GetMessageBody(bodyId, TestContext.CurrentContext.CancellationToken);
            var second = await MessagesViewStore.GetMessageBody(bodyId, TestContext.CurrentContext.CancellationToken);

            Assert.That(first.Version.HasValue, Is.True, "a body with no validator cannot be revalidated, so conditional GET is dead on it");
            Assert.That(second.Version, Is.EqualTo(first.Version), "the body did not change, so neither may its validator");
        }

        [Test]
        public async Task Does_respect_max_message_body()
        {
            await using var unitOfWork = await StartAuditUnitOfWork(1);

            var body = new byte[MAX_BODY_SIZE + 1000];
            Random.Shared.NextBytes(body);
            var processedMessage = MakeMessage();

            await unitOfWork.RecordProcessedMessage(processedMessage, body);

            await unitOfWork.Complete();

            var bodyId = GetBodyId(processedMessage);

            var retrievedMessage = await MessagesViewStore.GetMessageBody(bodyId, TestContext.CurrentContext.CancellationToken);

            Assert.That(retrievedMessage, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(retrievedMessage.Found, Is.True);
                Assert.That(retrievedMessage.HasContent, Is.False);
            }
        }

        [Test]
        public async Task Does_not_deduplicate_with_different_processing_started_header()
        {
            await using var unitOfWork = await StartAuditUnitOfWork(1);
            var messageId = "duplicatedId";
            var processingEndpoint = "endpoint";
            var processingStarted = DateTimeOffset.UtcNow;
            var duplicatedProcessingStarted = processingStarted.AddSeconds(5);

            var processedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: processingStarted);
            var duplicatedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: duplicatedProcessingStarted);
            await unitOfWork.RecordProcessedMessage(processedMessage);
            await unitOfWork.RecordProcessedMessage(duplicatedMessage);

            await unitOfWork.Complete();

            await configuration.CompleteDBOperation();

            var queryResult = await MessagesViewStore.GetMessages(false, new PagingInfo(), new SortInfo("message_id", "asc"), cancellationToken: TestContext.CurrentContext.CancellationToken);

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
            DateTimeOffset? processingStarted = null,
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
                { Headers.ProcessingStarted, DateTimeOffsetHelper.ToWireFormattedString(processingStarted ?? DateTimeOffset.UtcNow) },
                { Headers.EnclosedMessageTypes, messageType }
            };


            return new ProcessedMessage(headers, metadata);
        }

        async Task IngestProcessedMessagesAudits(params ProcessedMessage[] processedMessages)
        {
            await using var unitOfWork = await StartAuditUnitOfWork(processedMessages.Length);
            foreach (var processedMessage in processedMessages)
            {
                await unitOfWork.RecordProcessedMessage(processedMessage);
            }

            await unitOfWork.Complete();
            await configuration.CompleteDBOperation();
        }

        const int MAX_BODY_SIZE = 20536;
    }
}