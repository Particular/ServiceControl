namespace ServiceControl.UnitTests.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using MessageAuditing;
    using MessageFailures;
    using NUnit.Framework;
    using Raven.Client.Embedded;
    using ServiceControl.Infrastructure.RavenDB.Expiration;
    using ServiceControl.Operations.BodyStorage.RavenAttachments;
    using ServiceControl.SagaAudit;

    [TestFixture]
    public class CustomExpirationBundleTests
    {
        [Test]
        public void Old_documents_are_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var processedMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = DateTime.UtcNow,
                };

                var sagaHistoryId = Guid.NewGuid();
                var sagaHistory = new SagaHistory
                {
                    Id = sagaHistoryId,
                };
                
                using (var session = documentStore.OpenSession())
                {
                    session.Store(processedMessage);
                    session.Store(sagaHistory);
                    session.SaveChanges();
                }

                RunExpiry(documentStore, DateTime.UtcNow);

                using (var session = documentStore.OpenSession())
                {
                    Assert.IsEmpty(session.Query<ProcessedMessage>());
                    Assert.IsEmpty(session.Query<SagaHistory>());
                }
            }
        }

        [Test]
        public void Many_documents_are_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var expiredMessages = BuilExpiredMessaged().ToList();
                using (var session = documentStore.OpenSession())
                {
                    foreach (var message in expiredMessages)
                    {
                        session.Store(message);
                    }
                    session.SaveChanges();
                }
                var thresholdTime = DateTime.UtcNow;
                using (var session = documentStore.OpenSession())
                {
                    var recentMessage = new ProcessedMessage
                    {
                        Id = "recentMessageId",
                        ProcessedAt = DateTime.UtcNow,
                    };
                    session.Store(recentMessage);
                    var recentSagaHistory = new SagaHistory
                    {
                        Id = Guid.NewGuid(),
                    };
                    session.Store(recentSagaHistory);
                    session.SaveChanges();
                }

                RunExpiry(documentStore, thresholdTime);
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
                    Assert.AreEqual(1, session.Query<SagaHistory>().Count());
                }
            }
        }

        IEnumerable<object> BuilExpiredMessaged()
        {
            for (var i = 0; i < 10; i++)
            {
                yield return new ProcessedMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    ProcessedAt = DateTime.UtcNow,
                };
                yield return new SagaHistory
                {
                    Id = Guid.NewGuid(),
                };
            }
        }

        static void RunExpiry(EmbeddableDocumentStore documentStore, DateTime expiryThreshold)
        {
            new ExpiryProcessedMessageIndex().Execute(documentStore);
            documentStore.WaitForIndexing();
            ExpiredDocumentsCleaner.RunCleanup(100, documentStore.DocumentDatabase, expiryThreshold);
            documentStore.WaitForIndexing();
        }

        [Test]
        public void Only_processed_messages_are_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var expiredMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = DateTime.UtcNow,
                };

                var expiredSagaHistory = new SagaHistory
                {
                    Id = Guid.NewGuid(),
                };

                using (var session = documentStore.OpenSession())
                {
                    session.Store(expiredMessage);
                    session.Store(expiredSagaHistory);
                    session.SaveChanges();
                }
                var expiryThreshold = DateTime.UtcNow;
                var recentMessage = new ProcessedMessage
                {
                    Id = "2",
                    ProcessedAt = DateTime.UtcNow,
                };
                var recentSagaHistory = new SagaHistory
                {
                    Id = Guid.NewGuid(),
                };
                using (var session = documentStore.OpenSession())
                {
                    session.Store(recentMessage);
                    session.Store(recentSagaHistory);
                    session.SaveChanges();
                }

                RunExpiry(documentStore, expiryThreshold);

                using (var session = documentStore.OpenSession())
                {
                    Assert.Null(session.Load<ProcessedMessage>(expiredMessage.Id));
                    Assert.Null(session.Load<SagaHistory>(expiredSagaHistory.Id));
                    Assert.NotNull(session.Load<ProcessedMessage>(recentMessage.Id));
                    Assert.NotNull(session.Load<SagaHistory>(recentSagaHistory.Id));
                }
            }
        }

        [Test]
        public void Stored_bodies_are_being_removed_when_message_expires()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                // Store expired message with associated body
                var messageId = "21";

                var processedMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = DateTime.UtcNow
                };

                processedMessage.MessageMetadata["MessageId"] = messageId;

                using (var session = documentStore.OpenSession())
                {
                    session.Store(processedMessage);
                    session.SaveChanges();
                }

                var body = new byte[]
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };

                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };
                using (var stream = new MemoryStream(body))
                {
                    bodyStorage.Store(messageId, "binary", 5, stream);
                }
                RunExpiry(documentStore, DateTime.UtcNow);

                // Verify message expired
                using (var session = documentStore.OpenSession())
                {
                    Assert.Null(session.Load<ProcessedMessage>(processedMessage.Id));
                }

                // Verify body expired
                Stream dummy;
                Assert.False(bodyStorage.TryFetch(messageId, out dummy), "Audit document body should be deleted");
            }
        }

        [Test]
        public void Recent_processed_messages_are_not_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var expiryThreshold = DateTime.UtcNow.AddSeconds(-1);
                var processedMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = DateTime.UtcNow,
                };

                using (var session = documentStore.OpenSession())
                {
                    session.Store(processedMessage);
                    session.SaveChanges();
                }
                RunExpiry(documentStore, expiryThreshold);
                using (var session = documentStore.OpenSession())
                {
                    Assert.NotNull(session.Load<ProcessedMessage>(processedMessage.Id));
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
                }
                RunExpiry(documentStore, DateTime.UtcNow);

                using (var session = documentStore.OpenSession())
                {
                    Assert.NotNull(session.Load<FailedMessage>(failedMsg.Id));
                }
            }
        }

    }
}
