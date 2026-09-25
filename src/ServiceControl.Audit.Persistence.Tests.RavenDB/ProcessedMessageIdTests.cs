namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Auditing;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    [TestFixture]
    class ProcessedMessageIdTests : PersistenceTestFixture
    {
        [Test]
        public async Task Retains_existing_document_and_body_ids()
        {
            var started = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
            var message = new ProcessedMessage(
                new Dictionary<string, string>
                {
                    [Headers.MessageId] = "message-id",
                    [Headers.ProcessingEndpoint] = "endpoint",
                    [Headers.ProcessingStarted] = DateTimeOffsetHelper.ToWireFormattedString(started)
                },
                []);

            Assert.That(message.Id, Is.Null);

            await using var unitOfWork = await StartAuditUnitOfWork(1);
            await unitOfWork.RecordProcessedMessage(message, new byte[] { 42 });
            await unitOfWork.Complete();

            var expectedId = $"ProcessedMessages-{started.UtcDateTime.Ticks}-{message.GetProcessingId()}";
            Assert.That(message.Id, Is.EqualTo(expectedId));
            Assert.That(message.MessageMetadata["BodyUrl"], Is.EqualTo($"/messages/{expectedId}/body"));
            var body = await MessagesViewStore.GetMessageBody(expectedId);
            Assert.That(body.HasContent, Is.True);
            body.StreamContent.Dispose();
        }
    }
}
