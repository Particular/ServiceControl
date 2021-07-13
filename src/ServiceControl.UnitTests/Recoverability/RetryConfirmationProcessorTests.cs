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
    using Raven.Abstractions.Commands;
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
        public void Should_handle_multiple_retry_confirmations_in_the_error_ingestion()
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

            var messageContexts = new List<MessageContext>
            {
                messageContext,
                messageContext
            };

            var commands = Processor.Process(messageContexts);

            Assert.DoesNotThrowAsync(() => Store.AsyncDatabaseCommands.BatchAsync(commands));
        }

        [Test]
        public async Task Should_handle_multiple_legacy_audit_instance_retry_confirmations()
        {
            var retryConfirmation = new MarkMessageFailureResolvedByRetry
            {
                FailedMessageId = MessageId
            };

            await Handler.Handle(retryConfirmation, new TestableMessageHandlerContext());

            Assert.DoesNotThrowAsync(() => Handler.Handle(retryConfirmation, new TestableInvokeHandlerContext()));
        }

        const string MessageId = "83C73A86-A45E-4FDF-8C95-E292526166F5";

    }
}