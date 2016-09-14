namespace ServiceControl.UnitTests.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using MessageAuditing;
    using MessageFailures;
    using NServiceBus;
    using NUnit.Framework;
    using Raven.Client;
    using ServiceControl.Infrastructure.RavenDB.Expiration;
    using ServiceControl.Operations.BodyStorage;

    [TestFixture]
    public class ProcessedMessageExpirationTests
    {
        [Test]
        public void Old_documents_are_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var expiredDate = DateTime.UtcNow.AddDays(-3);
                var thresholdDate = DateTime.UtcNow.AddDays(-2);
                var processedMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = expiredDate
                };

                using (var session = documentStore.OpenSession())
                {
                    session.Store(processedMessage);
                    session.SaveChanges();
                }
                RunExpiry(documentStore, thresholdDate);

                using (var session = documentStore.OpenSession())
                {
                    Assert.IsEmpty(session.Query<ProcessedMessage>());
                }
            }
        }

        [Test]
        public void Many_documents_are_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var expiredDate = DateTime.UtcNow.AddDays(-3);
                var thresholdDate = DateTime.UtcNow.AddDays(-2);
                var recentDate = DateTime.UtcNow.AddDays(-1);
                var expiredMessages = BuilExpiredMessaged(expiredDate).ToList();
                using (var session = documentStore.OpenSession())
                {
                    foreach (var message in expiredMessages)
                    {
                        session.Store(message);
                    }
                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    var recentMessage = new ProcessedMessage
                    {
                        Id = "recentMessageId",
                        ProcessedAt = recentDate
                    };
                    session.Store(recentMessage);
                    session.SaveChanges();
                }
                RunExpiry(documentStore, thresholdDate);
                foreach (dynamic message in expiredMessages)
                {
                    using (var session = documentStore.OpenSession())
                    {
                        Assert.Null(session.Load<ProcessedMessage>(message.Id));
                    }
                }

                using (var session = documentStore.OpenSession())
                {
                    Assert.AreEqual(1, session.Query<ProcessedMessage>().Count());
                }
            }
        }

        IEnumerable<object> BuilExpiredMessaged(DateTime dateTime)
        {
            for (var i = 0; i < 10; i++)
            {
                yield return new ProcessedMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    ProcessedAt = dateTime
                };
            }
        }

        static void RunExpiry(IDocumentStore documentStore, DateTime expiryThreshold, IMessageBodyStore messageBodyStore = null)
        {
            new ExpiryProcessedMessageIndex().Execute(documentStore);
            documentStore.WaitForIndexing();

            AuditMessageCleaner.Clean(100, documentStore, expiryThreshold, new CancellationToken(), messageBodyStore ?? new FakeMessageBodyStore());
            documentStore.WaitForIndexing();
        }

        [Test]
        public void Only_processed_messages_are_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var expiredDate = DateTime.UtcNow.AddDays(-3);
                var thresholdDate = DateTime.UtcNow.AddDays(-2);
                var recentDate = DateTime.UtcNow.AddDays(-1);
                var expiredMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = expiredDate
                };

                using (var session = documentStore.OpenSession())
                {
                    session.Store(expiredMessage);
                    session.SaveChanges();
                }

                var recentMessage = new ProcessedMessage
                {
                    Id = "2",
                    ProcessedAt = recentDate
                };
                using (var session = documentStore.OpenSession())
                {
                    session.Store(recentMessage);
                    session.SaveChanges();
                }
                RunExpiry(documentStore, thresholdDate);

                using (var session = documentStore.OpenSession())
                {
                    Assert.Null(session.Load<ProcessedMessage>(expiredMessage.Id));
                    Assert.NotNull(session.Load<ProcessedMessage>(recentMessage.Id));
                }
            }
        }

        [Test, Ignore]
        public void Stored_bodies_are_being_removed_when_message_expires()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var expiredDate = DateTime.UtcNow.AddDays(-3);
                var thresholdDate = DateTime.UtcNow.AddDays(-2);
                // Store expired message with associated body
                var messageId = "21";

                var processedMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = expiredDate,
                    Headers = new Dictionary<string, string>
                    {
                        {Headers.ContentType, "binary"},
                        {Headers.ProcessingEndpoint, "Boo"}
                    }

                };

                using (var session = documentStore.OpenSession())
                {
                    session.Store(processedMessage);
                    session.SaveChanges();
                }

                var messageBodyStore = new FakeMessageBodyStore();

                RunExpiry(documentStore, thresholdDate, messageBodyStore);

                // Verify message expired
                using (var session = documentStore.OpenSession())
                {
                    Assert.Null(session.Load<ProcessedMessage>(processedMessage.Id));
                }

                // Verify body expired
                Assert.AreEqual(messageId, messageBodyStore.DeletedMessages.SingleOrDefault(), "Audit document body should have been deleted");
            }
        }

        [Test]
        public void Recent_processed_messages_are_not_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var thresholdDate = DateTime.UtcNow.AddDays(-2);
                var recentDate = DateTime.UtcNow.AddDays(-1);
                var message = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = recentDate
                };
                using (var session = documentStore.OpenSession())
                {
                    session.Store(message);
                    session.SaveChanges();
                }
                RunExpiry(documentStore, thresholdDate);
                using (var session = documentStore.OpenSession())
                {
                    Assert.AreEqual(1, session.Query<ProcessedMessage>().Count());
                }
            }
        }

        [Test]
        public void Errors_are_not_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var failedMsg = new FailedMessage
                {
                    Id = "1",
                };

                using (var session = documentStore.OpenSession())
                {
                    session.Store(failedMsg);
                    session.SaveChanges();

                    Debug.WriteLine(session.Advanced.GetMetadataFor(failedMsg)["Last-Modified"]);
                }
                Thread.Sleep(100);
                RunExpiry(documentStore, DateTime.UtcNow);

                using (var session = documentStore.OpenSession())
                {
                    Assert.NotNull(session.Load<FailedMessage>(failedMsg.Id));
                }
            }
        }

        class FakeMessageBodyStore : IMessageBodyStore
        {
            public ClaimsCheck Store(string tag, byte[] messageBody, MessageBodyMetadata messageBodyMetadata, IMessageBodyStoragePolicy messageStoragePolicy)
            {
                throw new NotImplementedException();
            }

            public bool TryGet(string tag, string messageId, out byte[] messageBody, out MessageBodyMetadata messageBodyMetadata)
            {
                throw new NotImplementedException();
            }

            public void Delete(string tag, string messageId)
            {
                DeletedMessages.Add(messageId);
            }

            public IList<string> DeletedMessages = new List<string>();
        }

    }
}
