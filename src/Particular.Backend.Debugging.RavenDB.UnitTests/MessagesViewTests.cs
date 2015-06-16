namespace Particular.Backend.Debugging.RavenDB.UnitTests
{
    using System;
    using System.Linq;
    using NUnit.Framework;
    using Particular.Backend.Debugging.RavenDB.Api;
    using Particular.Backend.Debugging.RavenDB.Model;
    using Raven.Client;
    using Raven.Client.Linq;

    [TestFixture]
    public class MessagesViewTests : TestWithRavenDB
    {
        [Test]
        public void Filter_out_system_messages()
        {
            using (var session = documentStore.OpenSession())
            {
                var processedMessage = new MessageSnapshotDocument
                {
                    Id = "1",
                    IsSystemMessage = true,
                };

                session.Store(processedMessage);
                var processedMessage2 = new MessageSnapshotDocument
                {
                    Id = "2",
                    IsSystemMessage = false,
                };

                session.Store(processedMessage2);
                session.SaveChanges();
            }

            using (var session = documentStore.OpenSession())
            {
                var results = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .Where(x => !x.IsSystemMessage)
                    .OfType<MessageSnapshotDocument>()
                    .ToList();
                Assert.AreEqual(1, results.Count);
                Assert.AreNotEqual("1", results.Single().Id);
            }
        }

        [Test]
        public void Order_by_critical_time()
        {
            using (var session = documentStore.OpenSession())
            {
                session.Store(CreateMessageSnapshotDocumentWithCriticalTime("1", TimeSpan.FromSeconds(10)));
                session.Store(CreateMessageSnapshotDocumentWithCriticalTime("2", TimeSpan.FromSeconds(20)));
                session.Store(CreateMessageSnapshotDocumentWithCriticalTime("3", TimeSpan.FromSeconds(15)));
                session.Store(CreateMessageSnapshotDocumentWithCriticalTime("4", TimeSpan.FromSeconds(15)));
                session.SaveChanges();
            }

            WaitForIndexing(documentStore);

            using (var session = documentStore.OpenSession())
            {
                var firstByCriticalTime = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderBy(x => x.CriticalTime)
                    .Where(x => x.CriticalTime != null)
                    .AsProjection<MessageSnapshotDocument>()
                    .First();
                Assert.AreEqual("1", firstByCriticalTime.Id);

                var firstByCriticalTimeDesc = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderByDescending(x => x.CriticalTime)
                    .Where(x => x.CriticalTime != null)
                    .AsProjection<MessageSnapshotDocument>()
                    .First();
                Assert.AreEqual("2", firstByCriticalTimeDesc.Id);
            }
        }

        static MessageSnapshotDocument CreateMessageSnapshotDocumentWithCriticalTime(string id, TimeSpan criticalTime)
        {
            var document = new MessageSnapshotDocument
            {
                Id = id,
                Processing = new ProcessingStatistics
                {
                    CriticalTime = criticalTime
                }
            };
            return document;
        }

        [Test]
        public void Order_by_time_sent()
        {
            using (var session = documentStore.OpenSession())
            {
                session.Store(CreateMessageSnapshotDocumentWithTimeSent("1", DateTime.Today.AddSeconds(20)));
                session.Store(CreateMessageSnapshotDocumentWithTimeSent("2", DateTime.Today.AddSeconds(10)));
                session.Store(CreateMessageSnapshotDocumentWithTimeSent("3", DateTime.Today.AddDays(-1)));
                session.SaveChanges();
            }

            WaitForIndexing(documentStore);

            using (var session = documentStore.OpenSession())
            {
                var firstByTimeSent = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderBy(x => x.TimeSent)
                    .OfType<MessageSnapshotDocument>()
                    .First();
                Assert.AreEqual("3", firstByTimeSent.Id);

                var firstByTimeSentDesc = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderByDescending(x => x.TimeSent)
                    .OfType<MessageSnapshotDocument>()
                    .First();
                Assert.AreEqual("1", firstByTimeSentDesc.Id);
            }
        }

        static MessageSnapshotDocument CreateMessageSnapshotDocumentWithTimeSent(string id, DateTime timeSent)
        {
            var document = new MessageSnapshotDocument
            {
                Id = id,
                Processing = new ProcessingStatistics
                {
                    TimeSent = timeSent
                }
            };
            return document;
        }

        [SetUp]
        public void SetUp()
        {
            documentStore = InMemoryStoreBuilder.GetInMemoryStore();

            var customIndex = new MessagesViewIndex();
            customIndex.Execute(documentStore);

            var transformer = new MessagesViewTransformer();

            transformer.Execute(documentStore);
        }

        [TearDown]
        public void TearDown()
        {
            documentStore.Dispose();
        }

        IDocumentStore documentStore;
    }
}