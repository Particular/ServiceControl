namespace ServiceControl.Audit.Persistence.Tests
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

        ProcessedMessage MakeMessage(
            string messageId = "SomeId",
            MessageIntentEnum intent = MessageIntentEnum.Send
        )
        {
            var metadata = new Dictionary<string, object>
            {
                { "MessageId", messageId },
                { "MessageIntent", intent },
                { "CriticalTime", TimeSpan.FromSeconds(5) },
                { "ProcessingTime", TimeSpan.FromSeconds(1) },
                { "DeliveryTime", TimeSpan.FromSeconds(4) },
                { "IsSystemMessage", false },
                { "ContentLength", 25 }
            };

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, messageId },
                { "ServiceControl.Retry.UniqueMessageId", "someId" },
                { Headers.MessageIntent, intent.ToString() }
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