namespace Particular.Backend.Debugging.RavenDB.UnitTests
{
    using System;
    using NUnit.Framework;
    using Particular.Backend.Debugging.RavenDB.Expiration;
    using Particular.Backend.Debugging.RavenDB.Model;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;

    [TestFixture]
    public class ExpiredDocumentsCleanerTest : TestWithRavenDB
    {
        [Test]
        public void Processed_messages_older_than_threshold_are_expired()
        {
            var now = DateTime.UtcNow;
            var processedMessage = new MessageSnapshotDocument
            {
                Id = "1",
                ProcessedAt = now.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
                Status = MessageStatus.Successful
            };

            var archivedFailure = new MessageSnapshotDocument
            {
                Id = "2",
                ProcessedAt = now.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
                Status = MessageStatus.ArchivedFailure
            };

            var resolvedFailure = new MessageSnapshotDocument
            {
                Id = "3",
                ProcessedAt = now.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
                Status = MessageStatus.ResolvedSuccessfully
            };

            using (var session = documentStore.OpenSession())
            {
                session.Store(processedMessage);
                session.Store(archivedFailure);
                session.Store(resolvedFailure);
                session.SaveChanges();
            }

            RunExpiry(now);

            using (var session = documentStore.OpenSession())
            {
                Assert.IsNull(session.Load<MessageSnapshotDocument>(processedMessage.Id));
                Assert.IsNull(session.Load<MessageSnapshotDocument>(archivedFailure.Id));
                Assert.IsNull(session.Load<MessageSnapshotDocument>(resolvedFailure.Id));
            }
        }


        [Test]
        public void Processed_messages_younger_than_threshold_are_not_expired()
        {
            var now = DateTime.UtcNow;
            var processedMessage = new MessageSnapshotDocument
            {
                Id = "1",
                ProcessedAt = now.AddMinutes(-5),
            };

            using (var session = documentStore.OpenSession())
            {
                session.Store(processedMessage);
                session.SaveChanges();
            }

            RunExpiry(now);

            using (var session = documentStore.OpenSession())
            {
                var msg = session.Load<MessageSnapshotDocument>(processedMessage.Id);
                Assert.IsNotNull(msg);
            }
        }

        [Test]
        public void Failed_messages_older_than_threshold_are_not_expired()
        {
            var now = DateTime.UtcNow;
            var failedMessage = new MessageSnapshotDocument
            {
                Id = "1",
                ProcessedAt = now.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
                Status = MessageStatus.Failed
            };

            var repeatedlyFailedMessage = new MessageSnapshotDocument
            {
                Id = "2",
                ProcessedAt = now.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
                Status = MessageStatus.RepeatedFailure
            };

            using (var session = documentStore.OpenSession())
            {
                session.Store(failedMessage);
                session.Store(repeatedlyFailedMessage);
                session.SaveChanges();
            }

            RunExpiry(now);

            using (var session = documentStore.OpenSession())
            {
                Assert.IsNotNull(session.Load<MessageSnapshotDocument>(failedMessage.Id));
                Assert.IsNotNull(session.Load<MessageSnapshotDocument>(repeatedlyFailedMessage.Id));
            }
        }

        void RunExpiry(DateTime now)
        {
            WaitForIndexing(documentStore);
            var cleaner = new ExpiredDocumentsCleaner();
            cleaner.TryClean(now, documentStore.DocumentDatabase, 100);
        }

        [SetUp]
        public void SetUp()
        {
            documentStore = InMemoryStoreBuilder.GetInMemoryStore(true);

            var customIndex = new ExpiryProcessedMessageIndex();
            customIndex.Execute(documentStore);
        }

        [TearDown]
        public void TearDown()
        {
            documentStore.Dispose();
        }

        EmbeddableDocumentStore documentStore;
    }
}
