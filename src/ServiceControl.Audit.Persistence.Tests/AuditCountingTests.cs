namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.Approvals;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Monitoring;

    [TestFixture]
    class AuditCountingTests : PersistenceTestFixture
    {
        [Test]
        public async Task ShouldCountMessages()
        {
            var currentTime = new DateTime(2022, 02, 02, 1, 2, 3, DateTimeKind.Utc);
            var yesterday = new DateTime(2022, 02, 01, 4, 5, 6, DateTimeKind.Utc);
            var weekBefore = yesterday.AddDays(-7);
            var thirtyDaysAgo = yesterday.AddDays(-30);
            var fortyDaysAgo = yesterday.AddDays(-40);

            var messages = new[]
            {
                // 1 EndpointA day1 + 1 system message
                MakeMessage("EndpointA", currentTime, false),
                MakeMessage("EndpointA", currentTime, true),

                // 1 only system message
                MakeMessage("SystemMessage", currentTime, true),

                // 2 EndpointA day2
                MakeMessage("EndpointA", yesterday, false),
                MakeMessage("EndpointA", yesterday, false),

                // 3 EndpointB day1
                MakeMessage("EndpointB", currentTime, false),
                MakeMessage("EndpointB", currentTime, false),
                MakeMessage("EndpointB", currentTime, false),

                // 4 EndpointB a week before
                MakeMessage("EndpointB", weekBefore, false),
                MakeMessage("EndpointB", weekBefore, false),
                MakeMessage("EndpointB", weekBefore, false),
                MakeMessage("EndpointB", weekBefore, false),
            };

            await IngestProcessedMessagesAudits(messages);

            var resultContainer = await DataStore.QueryAuditCounts();
            var result = resultContainer.Results;

            // Not implemented in RavenDB 3.5
            if (DataStore.GetType().Assembly.GetName().Name == "ServiceControl.Audit.Persistence.RavenDb")
            {
                Assert.That(result.Count, Is.EqualTo(0));
                return;
            }

            Approver.Verify(result);
        }

        [Test]
        public async Task ShouldCountAuditedMessages()
        {
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);
            var weekBefore = yesterday.AddDays(-7);

            var messages = new[]
            {
                // 1 EndpointA day1 + 1 system message
                MakeMessage("EndpointA", today, false),
                MakeMessage("EndpointA", today, true),

                // 1 only system message
                MakeMessage("SystemMessage", today, true),

                // 2 EndpointA day2
                MakeMessage("EndpointA", yesterday, false),
                MakeMessage("EndpointA", yesterday, false),

                // 3 EndpointB day1
                MakeMessage("EndpointB", today, false),
                MakeMessage("EndpointB", today, false),
                MakeMessage("EndpointB", today, false),

                // 4 EndpointB a week before
                MakeMessage("EndpointB", weekBefore, false),
                MakeMessage("EndpointB", weekBefore, false),
                MakeMessage("EndpointB", weekBefore, false),
                MakeMessage("EndpointB", weekBefore, false),
            };

            await IngestProcessedMessagesAudits(messages);

            var endpointA = (await DataStore.QueryAuditCounts("EndpointA")).Results;
            var endpointB = (await DataStore.QueryAuditCounts("EndpointB")).Results;
            var sysMsgEndpoint = (await DataStore.QueryAuditCounts("SystemEndpoint")).Results;

            Assert.That(sysMsgEndpoint, Is.Empty);

            string ScrubDates(string input)
            {
                return input
                    .Replace(today.ToString("yyyy-MM-dd"), "(TODAY)")
                    .Replace(yesterday.ToString("yyyy-MM-dd"), "(YESTERDAY)")
                    .Replace(weekBefore.ToString("yyyy-MM-dd"), "(WEEKBEFORE)");
            }

            Approver.Verify(new
            {
                EndpointA = endpointA,
                EndpointB = endpointB
            }, ScrubDates);
        }

        ProcessedMessage MakeMessage(string processingEndpoint, DateTime processedAt, bool systemMessage)
        {
            var messageId = Guid.NewGuid().ToString();
            var messageType = "MyMessageType";
            var processingTime = TimeSpan.FromSeconds(1);
            var processingStarted = processedAt - processingTime;

            var metadata = new Dictionary<string, object>
            {
                { "MessageId", Guid.NewGuid().ToString() },
                { "MessageIntent", MessageIntentEnum.Send },
                { "CriticalTime", TimeSpan.FromSeconds(5) },
                { "ProcessingTime", processingTime },
                { "DeliveryTime", TimeSpan.FromSeconds(4) },
                { "IsSystemMessage", systemMessage },
                { "MessageType", messageType },
                { "IsRetried", false },
                { "ConversationId", messageId },
                { "ReceivingEndpoint", new EndpointDetails { Name = processingEndpoint } }
            };

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, messageId },
                { Headers.ProcessingEndpoint, processingEndpoint },
                { Headers.MessageIntent, nameof(MessageIntentEnum.Send) },
                { Headers.ConversationId, messageId },
                { Headers.ProcessingStarted, DateTimeExtensions.ToWireFormattedString(processingStarted) },
                { Headers.ProcessingEnded, DateTimeExtensions.ToWireFormattedString(processedAt) },
                { Headers.EnclosedMessageTypes, messageType }
            };

            return new ProcessedMessage(headers, metadata);
        }

        async Task IngestProcessedMessagesAudits(params ProcessedMessage[] processedMessages)
        {
            var unitOfWork = StartAuditUnitOfWork(processedMessages.Length);
            foreach (var processedMessage in processedMessages)
            {
                await unitOfWork.RecordProcessedMessage(processedMessage);
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }
    }
}
