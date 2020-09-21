using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Testing;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.TestDriver;
using ServiceControl.Contracts.Operations;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.MessageFailures.Handlers;
using ServiceControl.MessageFailures.InternalMessages;
using ServiceControl.UnitTests.Operations;

namespace ServiceControl.UnitTests.Archiving
{
    [TestFixture]
    public class UnArchiveMessagesByRangeHandlerTests : RavenTestDriver
    {
        private IDocumentStore Store { get; set; }

        [SetUp]
        public void SetUp()
        {
            Store = GetDocumentStore();
        }

        protected override void PreInitialize(IDocumentStore documentStore)
        {
            base.PreInitialize(documentStore);
            documentStore.Conventions.SaveEnumsAsIntegers = true;
        }

        [Test]
        public async Task Should_unarchive_messages_within_the_range()
        {
            var domainEvents = new FakeDomainEvents();
            var handler = new UnArchiveMessagesByRangeHandler(Store, domainEvents);

            var firstMessageId = await CreateMessage().ConfigureAwait(true);
            Thread.Sleep(500);
            var startDate = DateTime.UtcNow;
            var impactedMessageId = await CreateMessage().ConfigureAwait(true);
            Thread.Sleep(500);
            var endDate = DateTime.UtcNow;
            var thirdMessageId = await CreateMessage().ConfigureAwait(true);

            new FailedMessageViewIndex().Execute(Store);
            WaitForIndexing(Store);

            var context = new TestableMessageHandlerContext();
            var message = new UnArchiveMessagesByRange { From = startDate, To = endDate};

            await handler.Handle(message, context).ConfigureAwait(true);

            using (var session = Store.OpenAsyncSession())
            {
                var firstMessage = await session.LoadAsync<FailedMessage>(firstMessageId).ConfigureAwait(true);
                var impactedMessage = await session.LoadAsync<FailedMessage>(impactedMessageId).ConfigureAwait(true);
                var thirdMessage = await session.LoadAsync<FailedMessage>(thirdMessageId).ConfigureAwait(true);

                WaitForUserToContinueTheTest(Store);

                Assert.AreEqual(FailedMessageStatus.Unresolved, impactedMessage.Status);
                Assert.AreEqual(FailedMessageStatus.Archived, firstMessage.Status);
                Assert.AreEqual(FailedMessageStatus.Archived, thirdMessage.Status);
            }
        }

        private async Task<string> CreateMessage()
        {
            var documentId = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            var message = new FailedMessage()
            {
                Id = documentId, Status = FailedMessageStatus.Unresolved, ProcessingAttempts =
                    new List<FailedMessage.ProcessingAttempt>()
                    {
                        new FailedMessage.ProcessingAttempt()
                        {
                            MessageId = Guid.NewGuid().ToString(), AttemptedAt = DateTime.Now,
                            FailureDetails = new FailureDetails()
                                {AddressOfFailingEndpoint = "Address", TimeOfFailure = DateTime.Now},
                            MessageMetadata = new Dictionary<string, object>()
                            {
                                {"MessageId", messageId},
                                {"MessageType", "AType"},
                                {"TimeSent", DateTime.Now},
                                {"ReceivingEndpoint", new EndpointDetails() {Name = "Name"}}
                            }
                        }
                    }
            };

            using (var session = Store.OpenAsyncSession())
            {
                await session.StoreAsync(message).ConfigureAwait(true);
                await session.SaveChangesAsync().ConfigureAwait(true);
                message.Status = FailedMessageStatus.Archived;
                await session.SaveChangesAsync().ConfigureAwait(true);
            }

            return documentId;
        }

    }
}