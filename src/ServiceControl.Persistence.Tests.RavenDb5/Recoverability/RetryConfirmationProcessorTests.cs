namespace ServiceControl.UnitTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using MessageFailures;
    using MessageFailures.Handlers;
    using NServiceBus.Extensibility;
    using NServiceBus.Testing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using PersistenceTests;
    using ServiceControl.Operations;

    class RetryConfirmationProcessorTests : PersistenceTestBase
    {
        RetryConfirmationProcessor Processor { get; set; }
        LegacyMessageFailureResolvedHandler Handler { get; set; }

        [SetUp]
        public async Task Setup()
        {
            var domainEvents = new FakeDomainEvents();
            Processor = new RetryConfirmationProcessor(domainEvents);

            Handler = new LegacyMessageFailureResolvedHandler(ErrorMessageDataStore, domainEvents);

            await ErrorMessageDataStore.StoreFailedMessagesForTestsOnly(
                new FailedMessage
                {
                    Id = MessageId,
                    Status = FailedMessageStatus.Unresolved
                }
            );

            // TODO: Strange... I just commented these lines and the tests are still green....
            // David: I'm concerned by this, it suggests there's no data for the tests and the they succeed just because there's nothing to do

            //var retryDocumentCommands = RetryDocumentDataStore.CreateFailedMessageRetryDocument(Guid.NewGuid().ToString(), MessageId);
            //await GetRequiredService<Raven.Client.IDocumentStore>().AsyncDatabaseCommands.BatchAsync(new[] { retryDocumentCommands });
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

            Assert.DoesNotThrowAsync(() => unitOfWork.Complete());
        }

        [Test]
        public async Task Should_handle_multiple_legacy_audit_instance_retry_confirmations()
        {
            await Handler.Handle(CreateLegacyRetryConfirmationCommand(), new TestableMessageHandlerContext());

            Assert.DoesNotThrowAsync(
                () => Handler.Handle(CreateLegacyRetryConfirmationCommand(), new TestableInvokeHandlerContext()));
        }

        [Test]
        public async Task Should_handle_retry_confirmation_followed_by_legacy_command()
        {
            var messageContexts = new List<MessageContext>
            {
                CreateRetryAcknowledgementMessage()
            };

            var unitOfWork = await UnitOfWorkFactory.StartNew();
            await Processor.Process(messageContexts, unitOfWork);
            await unitOfWork.Complete();

            Assert.DoesNotThrowAsync(
                () => Handler.Handle(CreateLegacyRetryConfirmationCommand(), new TestableInvokeHandlerContext()));
        }

        [Test]
        public async Task Should_handle_legacy_retry_confirmation_command_followed_by_new_acknowledgement()
        {
            await Handler.Handle(CreateLegacyRetryConfirmationCommand(), new TestableMessageHandlerContext());

            var messageContexts = new List<MessageContext>
            {
                CreateRetryAcknowledgementMessage()
            };

            var unitOfWork = await UnitOfWorkFactory.StartNew();
            await Processor.Process(messageContexts, unitOfWork);
            Assert.DoesNotThrowAsync(() => unitOfWork.Complete());
        }

        static MarkMessageFailureResolvedByRetry CreateLegacyRetryConfirmationCommand()
        {
            var retryConfirmation = new MarkMessageFailureResolvedByRetry
            {
                FailedMessageId = MessageId
            };
            return retryConfirmation;
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
                Array.Empty<byte>(),
                new TransportTransaction(),
                new CancellationTokenSource(),
                new ContextBag());
            return messageContext;
        }

        const string MessageId = "83C73A86-A45E-4FDF-8C95-E292526166F5";
    }
}