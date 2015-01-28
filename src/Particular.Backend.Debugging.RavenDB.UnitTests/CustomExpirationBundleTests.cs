namespace Particular.Backend.Debugging.RavenDB.UnitTests
{
    using System;
    using NUnit.Framework;
    using Particular.Backend.Debugging.RavenDB.Data;
    using Particular.Backend.Debugging.RavenDB.Expiration;
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
            var processedMessage = new ProdDebugMessage
            {
                Id = "1",
                AttemptedAt = now.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
                Status = MessageStatus.Successful
            };
            
            var archivedFailure = new ProdDebugMessage
            {
                Id = "2",
                AttemptedAt = now.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
                Status = MessageStatus.ArchivedFailure
            }; 

            var resolvedFailure = new ProdDebugMessage
            {
                Id = "3",
                AttemptedAt = now.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
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
                Assert.IsNull(session.Load<ProdDebugMessage>(processedMessage.Id));
                Assert.IsNull(session.Load<ProdDebugMessage>(archivedFailure.Id));
                Assert.IsNull(session.Load<ProdDebugMessage>(resolvedFailure.Id));
            }
        }


        [Test]
        public void Processed_messages_younger_than_threshold_are_not_expired()
        {
            var now = DateTime.UtcNow;
            var processedMessage = new ProdDebugMessage
            {
                Id = "1",
                AttemptedAt = now.AddMinutes(-5),
            };

            using (var session = documentStore.OpenSession())
            {
                session.Store(processedMessage);
                session.SaveChanges();
            }

            RunExpiry(now);

            using (var session = documentStore.OpenSession())
            {
                var msg = session.Load<ProdDebugMessage>(processedMessage.Id);
                Assert.IsNotNull(msg);
            }
        }

        [Test]
        public void Failed_messages_older_than_threshold_are_not_expired()
        {
            var now = DateTime.UtcNow;
            var failedMessage = new ProdDebugMessage
            {
                Id = "1",
                AttemptedAt = now.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
                Status = MessageStatus.Failed
            };

            var repeatedlyFailedMessage = new ProdDebugMessage
            {
                Id = "2",
                AttemptedAt = now.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
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
                Assert.IsNotNull(session.Load<ProdDebugMessage>(failedMessage.Id));
                Assert.IsNotNull(session.Load<ProdDebugMessage>(repeatedlyFailedMessage.Id));
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
