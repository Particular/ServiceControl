namespace ServiceControl.Persistence.Tests.RavenDB.Unarchiving
{
    using System;
    using System.Threading.Tasks;
    using MessageFailures;
    using MessageFailures.Api;
    using NUnit.Framework;
    using Raven.Client.Documents.Operations.Indexes;
    using ServiceControl.Persistence;

    [TestFixture]
    class UnarchiveByRangeTests : RavenPersistenceTestBase
    {
        [Test]
        public async Task Should_unarchive_a_message_archived_right_before_the_request()
        {
            var uniqueMessageId = Guid.NewGuid().ToString();
            var documentId = FailedMessageIdGenerator.MakeDocumentId(uniqueMessageId);
            var indexName = new FailedMessageViewIndex().IndexName;

            using (var session = DocumentStore.OpenAsyncSession())
            {
                await session.StoreAsync(FailedMessageBuilder.Minimal(message =>
                {
                    message.Id = documentId;
                    message.UniqueMessageId = uniqueMessageId;
                }));
                await session.SaveChangesAsync();
            }

            await CompleteDatabaseOperation();

            // The index still has the message as unresolved when the range is unarchived
            await DocumentStore.Maintenance.SendAsync(new StopIndexOperation(indexName));

            try
            {
                using (var session = DocumentStore.OpenAsyncSession())
                {
                    var message = await session.LoadAsync<FailedMessage>(documentId);
                    message.Status = FailedMessageStatus.Archived;
                    await session.SaveChangesAsync();
                }

                var unarchive = FailedMessageLifecycleStore.UnArchiveMessagesByRange(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));

                await Task.Delay(TimeSpan.FromSeconds(1));
                await DocumentStore.Maintenance.SendAsync(new StartIndexOperation(indexName));

                var unarchived = await unarchive;

                using var verifySession = DocumentStore.OpenAsyncSession();
                var unarchivedMessage = await verifySession.LoadAsync<FailedMessage>(documentId);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(unarchived, Is.EquivalentTo(new[] { documentId }));
                    Assert.That(unarchivedMessage.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
                }
            }
            finally
            {
                await DocumentStore.Maintenance.SendAsync(new StartIndexOperation(indexName));
            }
        }
    }
}
