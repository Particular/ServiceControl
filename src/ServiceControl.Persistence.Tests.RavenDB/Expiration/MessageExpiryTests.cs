namespace ServiceControl.Persistence.Tests.RavenDB.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EventLog;
    using MessageFailures;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Persistence.Infrastructure;
    using Raven.Client.Documents.Operations.Expiration;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Persistence.Tests.RavenDB;

    [TestFixture]
    public class MessageExpiryTests : RavenPersistenceTestBase
    {
        [SetUp]
        public async Task Setup()
        {
            var settings = (RavenPersisterSettings)PersistenceSettings;

            settings.ErrorRetentionPeriod = TimeSpan.FromMilliseconds(1);
            settings.EventsRetentionPeriod = TimeSpan.FromMilliseconds(1);

            await EnableExpiration();
        }

        [Test]
        public async Task SingleMessageMarkedAsArchiveShouldExpire()
        {
            var (context, attempt) = CreateMessageContext();

            using (var uow = await IngestionUnitOfWorkFactory.StartNew())
            {
                await uow.Recoverability.RecordFailedProcessingAttempt(context, attempt, []);

                await uow.Complete(TestContext.CurrentContext.CancellationToken);
            }

            CompleteDatabaseOperation();

            var error = await GetAllMessages();

            Assert.That(error.Results, Has.Count.EqualTo(1), "Failed message should be available to query after ingestion");

            await ErrorStore.FailedMessageMarkAsArchived(error.Results.First().Id);

            await WaitUntil(async () => (await GetAllMessages()).Results.Count == 0, "Archived message should be removed after archiving.");
        }

        async Task<QueryResult<IList<MessagesView>>> GetAllMessages() => await ErrorStore.GetAllMessages(new PagingInfo(1, 10), new SortInfo(null, null), false);

        [Test]
        public async Task AllMessagesInUnArchivedGroupShouldNotExpire()
        {
            var groupIdA = Guid.NewGuid().ToString();
            var groupIdB = Guid.NewGuid().ToString();

            var (contextA, attemptA) = CreateMessageContext();
            var (contextB, attemptB) = CreateMessageContext();

            using (var uow = await IngestionUnitOfWorkFactory.StartNew())
            {
                await uow.Recoverability.RecordFailedProcessingAttempt(contextA, attemptA, [new FailedMessage.FailureGroup { Id = groupIdA }]);
                await uow.Recoverability.RecordFailedProcessingAttempt(contextB, attemptB, [new FailedMessage.FailureGroup { Id = groupIdB }]);

                await uow.Complete(TestContext.CurrentContext.CancellationToken);
            }

            CompleteDatabaseOperation();

            var error = await GetAllMessages();

            Assert.That(error.Results, Has.Count.EqualTo(2), "Failed message should be available to query after ingestion");

            await DisableExpiration();

            await ArchiveMessages.ArchiveAllInGroup(groupIdA);

            await ArchiveMessages.ArchiveAllInGroup(groupIdB);
            await ArchiveMessages.UnarchiveAllInGroup(groupIdB);

            await EnableExpiration();

            await WaitUntil(async () => (await GetAllMessages()).Results.Count == 1, "Unarchived message should not be removed.");
        }

        [Test]
        public async Task AllMessagesInArchivedGroupShouldExpire()
        {
            var groupId = Guid.NewGuid().ToString();
            var (context, attempt) = CreateMessageContext();

            using (var uow = await IngestionUnitOfWorkFactory.StartNew())
            {
                await uow.Recoverability.RecordFailedProcessingAttempt(context, attempt, [new FailedMessage.FailureGroup { Id = groupId }]);

                await uow.Complete(TestContext.CurrentContext.CancellationToken);
            }

            CompleteDatabaseOperation();

            var error = await GetAllMessages();

            Assert.That(error.Results, Has.Count.EqualTo(1), "Failed message should be available to query after ingestion");

            await ArchiveMessages.ArchiveAllInGroup(groupId);

            await WaitUntil(async () => (await GetAllMessages()).Results.Count == 0, "Archived message should be removed after archiving.");
        }

        [Test]
        public async Task SingleMessageMarkedAsResolvedShouldExpire()
        {
            var (context, attempt) = CreateMessageContext();

            using (var uow = await IngestionUnitOfWorkFactory.StartNew())
            {
                await uow.Recoverability.RecordFailedProcessingAttempt(context, attempt, []);

                await uow.Complete(TestContext.CurrentContext.CancellationToken);
            }

            CompleteDatabaseOperation();

            var errors = await GetAllMessages();

            Assert.That(errors.Results, Has.Count.EqualTo(1), "Failed message should be available to query after ingestion");

            await ErrorStore.MarkMessageAsResolved(errors.Results.First().Id);

            await WaitUntil(async () => (await GetAllMessages()).Results.Count == 0, "Archived message should be removed after archiving.");
        }

        [Test]
        public async Task RetryConfirmationProcessingShouldTriggerExpiration()
        {
            var (context, attempt) = CreateMessageContext();

            using (var uow = await IngestionUnitOfWorkFactory.StartNew())
            {
                await uow.Recoverability.RecordFailedProcessingAttempt(context, attempt, []);

                await uow.Complete(TestContext.CurrentContext.CancellationToken);
            }

            CompleteDatabaseOperation();

            var errors = await GetAllMessages();

            Assert.That(errors.Results, Has.Count.EqualTo(1), "Failed message should be available to query after ingestion");

            using (var uow = await IngestionUnitOfWorkFactory.StartNew())
            {
                await uow.Recoverability.RecordSuccessfulRetry(errors.Results.First().Id);

                await uow.Complete(TestContext.CurrentContext.CancellationToken);
            }

            await WaitUntil(async () => (await GetAllMessages()).Results.Count == 0, "Retry confirmation should cause message removal.");
        }

        static (MessageContext, FailedMessage.ProcessingAttempt) CreateMessageContext()
        {
            var headers = new Dictionary<string, string>
            {
                {Headers.ProcessingEndpoint, "SomeEndpoint"},
                {Headers.MessageId, Guid.NewGuid().ToString() }
            };

            var attempt = FailedMessageBuilder.Minimal().ProcessingAttempts.First();

            var message = new MessageContext(Guid.NewGuid().ToString(), headers, ReadOnlyMemory<byte>.Empty, new TransportTransaction(), "receiveAddress", new ContextBag());

            return (message, attempt);
        }

        [Test]
        public async Task EventLogItemShouldExpire()
        {
            await DisableExpiration();

            await ErrorStore.StoreEventLogItem(new EventLogItem());

            CompleteDatabaseOperation();

            var (logItems, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(1, 1));

            Assert.That(logItems, Has.Count.EqualTo(1), "Event log items should be available to query.");

            await EnableExpiration();

            await WaitUntil(async () =>
            {
                var (items, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(1, 1));

                return items.Count == 0;
            }, "Event log items should be removed after expiration period elapses.");
        }

        async Task EnableExpiration(int frequency = 1) =>
            await DocumentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(
                new ExpirationConfiguration
                {
                    Disabled = false,
                    DeleteFrequencyInSec = frequency
                }));

        async Task DisableExpiration() =>
            await DocumentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(new ExpirationConfiguration { Disabled = true }));

        [TearDown]
        public async Task Teardown() => await EnableExpiration(60);
    }
}