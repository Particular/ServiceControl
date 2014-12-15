namespace ServiceControl.UnitTests.CompositeViews
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using MessageAuditing;
    using NUnit.Framework;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.CompositeViews.Messages;
    using FailedMessage = ServiceControl.MessageAuditing.FailedMessage;

    [TestFixture]
    public class MessagesViewTests 
    {
        [Test]
        public void Filter_out_system_messages()
        {
            using (var session = documentStore.OpenSession())
            {
                var processedMessage = new ProcessedMessage
                                       {
                                           Id = "1",
                                       };

                processedMessage.MessageMetadata["IsSystemMessage"] = true;
                session.Store(processedMessage);
                var processedMessage2 = new ProcessedMessage
                                        {
                                            Id = "2",
                                        };

                processedMessage2.MessageMetadata["IsSystemMessage"] = false;
                session.Store(processedMessage2);
                session.SaveChanges();
            }

            using (var session = documentStore.OpenSession())
            {
                var results = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .Where(x => !x.IsSystemMessage)
                    .OfType<ProcessedMessage>()
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
                session.Store(new ProcessedMessage
                {
                    Id = "1",
                    MessageMetadata = new Dictionary<string, object> { { "CriticalTime", TimeSpan.FromSeconds(10) } }
                });

                session.Store(new ProcessedMessage
                {
                    Id = "2",
                    MessageMetadata = new Dictionary<string, object> { { "CriticalTime", TimeSpan.FromSeconds(20) } }
                });

                session.Store(new ProcessedMessage
                {
                    Id = "3",
                    MessageMetadata = new Dictionary<string, object> { { "CriticalTime", TimeSpan.FromSeconds(15) } }
                });

                session.Store(new FailedMessage
                {
                    Id = "4",
                    Status = MessageStatus.Failed,
                    LastProcessingAttempt = new FailedMessage.ProcessingAttempt{MessageMetadata = new Dictionary<string, object> { { "CriticalTime", TimeSpan.FromSeconds(15) } }
                    },
                });
                session.SaveChanges();
            }

            documentStore.WaitForIndexing();

            using (var session = documentStore.OpenSession())
            {
                var firstByCriticalTime = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderBy(x => x.CriticalTime)
                    .Where(x => x.CriticalTime != null)
                    .AsProjection<ProcessedMessage>()
                    .First();
                Assert.AreEqual("1", firstByCriticalTime.Id);

                var firstByCriticalTimeDescription = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderByDescending(x => x.CriticalTime)
                    .Where(x => x.CriticalTime != null)
                    .AsProjection<ProcessedMessage>()
                    .First();
                Assert.AreEqual("2", firstByCriticalTimeDescription.Id);
            }
        }
        [Test]
        public void Order_by_time_sent()
        {
            using (var session = documentStore.OpenSession())
            {
                session.Store(new ProcessedMessage
                {
                    Id = "1",
                    MessageMetadata = new Dictionary<string, object> { { "TimeSent", DateTime.Today.AddSeconds(20) } }
                });

                session.Store(new ProcessedMessage
                {
                    Id = "2",
                    MessageMetadata = new Dictionary<string, object> { { "TimeSent", DateTime.Today.AddSeconds(10) } }
                });
                session.Store(new ProcessedMessage
                {
                    Id = "3",
                    MessageMetadata = new Dictionary<string, object> { { "TimeSent", DateTime.Today.AddDays(-1) } }
                });
                session.SaveChanges();
            }

            documentStore.WaitForIndexing();

            using (var session = documentStore.OpenSession())
            {
                var firstByTimeSent = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderBy(x => x.TimeSent)
                    .OfType<ProcessedMessage>()
                    .First();
                Assert.AreEqual("3", firstByTimeSent.Id);

                var firstByTimeSentDescription = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderByDescending(x => x.TimeSent)
                    .OfType<ProcessedMessage>()
                    .First();
                Assert.AreEqual("1", firstByTimeSentDescription.Id);
            }
        }

        [Test]
        public void Correct_status_for_repeated_errors()
        {
            using (var session = documentStore.OpenSession())
            {
                session.Store(new FailedMessage
                {
                    Id = "1",
                    LastProcessingAttempt = new FailedMessage.ProcessingAttempt{AttemptedAt = DateTime.Today, MessageMetadata = new Dictionary<string, object>{{"MessageIntent", "1"} }
                    },
                });
         
                session.SaveChanges();
            }

            documentStore.WaitForIndexing();

            using (var session = documentStore.OpenSession())
            {
                var message = session.Query<FailedMessage>()
                    .TransformWith<MessagesViewTransformer, MessagesView>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .Single();

                Assert.AreEqual(MessageStatus.RepeatedFailure, message.Status);
            }
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