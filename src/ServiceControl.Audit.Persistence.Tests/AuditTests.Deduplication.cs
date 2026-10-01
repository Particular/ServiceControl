namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    partial class AuditTests
    {
        [Test]
        public async Task Deduplicates_messages_in_same_batch()
        {
            await using var unitOfWork = await StartAuditUnitOfWork(1);
            var messageId = "duplicatedId";
            var processingEndpoint = "endpoint";
            var processingStarted = DateTimeOffset.UtcNow;

            var processedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: processingStarted);
            var duplicatedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: processingStarted);
            await unitOfWork.RecordProcessedMessage(processedMessage);
            await unitOfWork.RecordProcessedMessage(duplicatedMessage);

            await unitOfWork.Complete();

            await configuration.CompleteDBOperation();

            var queryResult = await MessagesViewStore.GetMessages(false, new PagingInfo(), new SortInfo("message_id", "asc"), cancellationToken: TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.QueryStats.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Deduplicates_messages_in_different_batches()
        {
            var messageId = "duplicatedId";
            var processingEndpoint = "endpoint";
            var processingStarted = DateTimeOffset.UtcNow;

            var processedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: processingStarted);
            await using var unitOfWork1 = await StartAuditUnitOfWork(1);
            await unitOfWork1.RecordProcessedMessage(processedMessage);
            await unitOfWork1.Complete();

            var duplicatedMessage = MakeMessage(messageId: messageId, processingEndpoint: processingEndpoint, processingStarted: processingStarted);
            await using var unitOfWork2 = await StartAuditUnitOfWork(1);
            await unitOfWork2.RecordProcessedMessage(duplicatedMessage);
            await unitOfWork2.Complete();

            await configuration.CompleteDBOperation();

            var queryResult = await MessagesViewStore.GetMessages(false, new PagingInfo(), new SortInfo("message_id", "asc"), cancellationToken: TestContext.CurrentContext.CancellationToken);

            Assert.That(queryResult.QueryStats.TotalCount, Is.EqualTo(1));
        }
    }
}
