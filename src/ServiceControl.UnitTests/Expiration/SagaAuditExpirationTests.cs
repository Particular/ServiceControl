namespace ServiceControl.UnitTests.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using MessageFailures;
    using NUnit.Framework;
    using Raven.Client.Embedded;
    using ServiceControl.Infrastructure.RavenDB.Expiration;
    using ServiceControl.SagaAudit;

    [TestFixture]
    public class SagaAuditExpirationTests
    {
        [Test]
        public void Old_documents_are_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var expiredDate = DateTime.UtcNow.AddDays(-3);
                var thresholdDate = DateTime.UtcNow.AddDays(-2);

                var sagaHistoryId = Guid.NewGuid();
                var sagaHistory = new SagaHistory
                {
                    Id = sagaHistoryId,
                };

                using (new RavenLastModifiedScope(expiredDate))
                using (var session = documentStore.OpenSession())
                {
                    session.Store(sagaHistory);
                    session.SaveChanges();
                }
                RunExpiry(documentStore, thresholdDate);

                using (var session = documentStore.OpenSession())
                {
                    Assert.IsEmpty(session.Query<SagaHistory>());
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
                var expiredMessages = BuilExpiredMessaged().ToList();
                using (new RavenLastModifiedScope(expiredDate))
                {
                    using (var session = documentStore.OpenSession())
                    {
                        foreach (var message in expiredMessages)
                        {
                            session.Store(message);
                        }
                        session.SaveChanges();
                    }
                }

                using (new RavenLastModifiedScope(recentDate))
                using (var session = documentStore.OpenSession())
                {
                    var recentSagaHistory = new SagaHistory
                    {
                        Id = Guid.NewGuid(),
                    };
                    session.Store(recentSagaHistory);
                    session.SaveChanges();
                }
                RunExpiry(documentStore, thresholdDate);

                using (var session = documentStore.OpenSession())
                {
                    Assert.AreEqual(1, session.Query<SagaHistory>().Count());
                }
            }
        }

        IEnumerable<object> BuilExpiredMessaged()
        {
            for (var i = 0; i < 10; i++)
            {
                yield return new SagaHistory
                {
                    Id = Guid.NewGuid(),
                };
            }
        }

        static void RunExpiry(EmbeddableDocumentStore documentStore, DateTime expiryThreshold)
        {
            new ExpirySagaAuditIndex().Execute(documentStore);
            documentStore.WaitForIndexing();
            SagaHistoryCleaner.Clean(100, documentStore.DocumentDatabase, expiryThreshold);
            documentStore.WaitForIndexing();
        }

        [Test]
        public void Only_expred_being_deleted()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var expiredDate = DateTime.UtcNow.AddDays(-3);
                var thresholdDate = DateTime.UtcNow.AddDays(-2);
                var recentDate = DateTime.UtcNow.AddDays(-1);

                var expiredSagaHistory = new SagaHistory
                {
                    Id = Guid.NewGuid(),
                };

                using (new RavenLastModifiedScope(expiredDate))
                using (var session = documentStore.OpenSession())
                {
                    session.Store(expiredSagaHistory);
                    session.SaveChanges();
                }

                var recentSagaHistory = new SagaHistory
                {
                    Id = Guid.NewGuid(),
                };
                using (new RavenLastModifiedScope(recentDate))
                using (var session = documentStore.OpenSession())
                {
                    session.Store(recentSagaHistory);
                    session.SaveChanges();
                }
                RunExpiry(documentStore, thresholdDate);

                using (var session = documentStore.OpenSession())
                {
                    Assert.Null(session.Load<SagaHistory>(expiredSagaHistory.Id));
                    Assert.NotNull(session.Load<SagaHistory>(recentSagaHistory.Id));
                }
            }
        }


        [Test]
        public void Recent_processed_messages_are_not_being_expired()
        {
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var thresholdDate = DateTime.UtcNow.AddDays(-2);
                var recentDate = DateTime.UtcNow.AddDays(-1);
                var sagaHistory = new SagaHistory
                {
                    Id = Guid.NewGuid(),
                };

                using (new RavenLastModifiedScope(recentDate))
                using (var session = documentStore.OpenSession())
                {
                    session.Store(sagaHistory);
                    session.SaveChanges();
                }
                RunExpiry(documentStore, thresholdDate);
                using (var session = documentStore.OpenSession())
                {
                    Assert.AreEqual(1, session.Query<SagaHistory>().Count());
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

    }
}
