namespace ServiceControl.UnitTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using MessageFailures.Handlers;
    using NServiceBus.Extensibility;
    using NServiceBus.Testing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Operations;
    using Raven.Client;
    using Raven.Tests.Helpers;
    using MessageFailures;
    using ServiceControl.Operations;
    using ServiceControl.Recoverability;

    public class RetryConfirmationProcessorTests : RavenTestBase
    {
        IDocumentStore Store { get; set; }
        RetryConfirmationProcessor Processor { get; set; }
        LegacyMessageFailureResolvedHandler Handler { get; set; }

        [SetUp]
        public async Task Setup()
        {
            Store = NewDocumentStore();

            var domainEvents = new FakeDomainEvents();
            Processor = new RetryConfirmationProcessor(domainEvents);

            var retryDocumentManager = new RetryDocumentManager(new FakeApplicationLifetime(), Store, new RetryingManager(domainEvents));
            Handler = new LegacyMessageFailureResolvedHandler(Store, domainEvents, retryDocumentManager);

            using (var session = Store.OpenAsyncSession())
            {
                var failedMessage = new FailedMessage
                {
                    Id = FailedMessage.MakeDocumentId(MessageId),
                    Status = FailedMessageStatus.Unresolved
                };

                await session.StoreAsync(failedMessage);
                await session.SaveChangesAsync();
            }

            var retryDocumentCommands = RetryDocumentManager.CreateFailedMessageRetryDocument(Guid.NewGuid().ToString(), MessageId);
            await Store.AsyncDatabaseCommands.BatchAsync(new[] { retryDocumentCommands });
        }

        [Test]
        public async Task Should_handle_multiple_retry_confirmations_in_the_error_ingestion()
        {
            var messageContexts = new List<MessageContext>
            {
                CreateRetryAcknowledgementMessage(),
                CreateRetryAcknowledgementMessage()
            };

            var unitOfWork = new RavenDbIngestionUnitOfWork(Store);
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

            var unitOfWork = new RavenDbIngestionUnitOfWork(Store);
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

            var unitOfWork = new RavenDbIngestionUnitOfWork(Store);
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
                new byte[0],
                new TransportTransaction(),
                new CancellationTokenSource(),
                new ContextBag());
            return messageContext;
        }

        const string MessageId = "83C73A86-A45E-4FDF-8C95-E292526166F5";

    }
}