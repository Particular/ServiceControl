namespace ServiceControl.Persistence.Tests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MessageFailures;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.Operations;

    class RetryConfirmationProcessorTests : PersistenceTestBase
    {
        RetryConfirmationProcessor Processor { get; set; }

        [SetUp]
        public async Task Setup()
        {
            var domainEvents = new FakeDomainEvents();
            Processor = new RetryConfirmationProcessor(domainEvents);

            await PersistenceTestsContext.InsertFailedMessages(
                new FailedMessage
                {
                    Id = MessageId,
                    Status = FailedMessageStatus.Unresolved
                }
            );

            var batchId = Guid.NewGuid().ToString();
            await RetryBatchStore.AssignMessagesToBatch(batchId, new[] { MessageId });
        }

        [Test]
        public async Task Should_handle_multiple_retry_confirmations_in_the_error_ingestion()
        {
            var messageContexts = new List<MessageContext>
            {
                CreateRetryAcknowledgementMessage(),
                CreateRetryAcknowledgementMessage()
            };

            var unitOfWork = await UnitOfWorkFactory.StartNew();
            await Processor.Process(messageContexts, unitOfWork);

            Assert.DoesNotThrowAsync(() => unitOfWork.Complete(TestContext.CurrentContext.CancellationToken));
        }

        static MessageContext CreateRetryAcknowledgementMessage()
        {
            var headers = new Dictionary<string, string>
            {
                {"ServiceControl.Retry.Successful", string.Empty},
                {"ServiceControl.Retry.UniqueMessageId", MessageId}
            };
            var messageContext = new MessageContext(
                MessageId,
                headers,
                ReadOnlyMemory<byte>.Empty,
                new TransportTransaction(),
                "receiveAddress",
                new ContextBag());
            return messageContext;
        }

        const string MessageId = "83C73A86-A45E-4FDF-8C95-E292526166F5";
    }
}
