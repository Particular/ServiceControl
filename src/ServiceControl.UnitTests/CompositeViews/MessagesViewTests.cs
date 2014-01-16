namespace ServiceControl.UnitTests.CompositeViews
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using MessageAuditing;
    using MessageFailures;
    using NUnit.Framework;
    using Raven.Client;
    using Raven.Client.Linq;
    using ServiceControl.CompositeViews.Messages;

    [TestFixture]
    public class MessagesViewTests
    {
        [Test]
        public void Filter_out_system_messages()
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

            var results = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .Customize(x => x.WaitForNonStaleResults())
                .Where(x => !x.IsSystemMessage)
               .OfType<ProcessedMessage>()
                .ToList();
            Assert.AreEqual(1, results.Count);
            Assert.AreNotEqual("1", results.Single().Id);
        }


        [Test]
        public void Order_by_critical_time()
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
            session.SaveChanges();

            var firstByCriticalTime = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .OrderBy(x => x.CriticalTime)
                .Customize(x => x.WaitForNonStaleResults())
                .OfType<ProcessedMessage>()
                .First();
            Assert.AreEqual("1", firstByCriticalTime.Id);
           
            var firstByCriticalTimeDesc = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                  .OrderByDescending(x => x.CriticalTime)
                  .Customize(x => x.WaitForNonStaleResults())
                  .OfType<ProcessedMessage>()
                  .First();
            Assert.AreEqual("2", firstByCriticalTimeDesc.Id);
        }
        [Test]
        public void Order_by_time_sent()
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

            var firstByTimeSent = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .OrderBy(x => x.TimeSent)
                .Customize(x => x.WaitForNonStaleResults())
                .OfType<ProcessedMessage>()
                .First();
            Assert.AreEqual("3", firstByTimeSent.Id);

            var firstByTimeSentDesc = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                  .OrderByDescending(x => x.CriticalTime)
                  .Customize(x => x.WaitForNonStaleResults())
                  .OfType<ProcessedMessage>()
                  .First();
            Assert.AreEqual("1", firstByTimeSentDesc.Id);
        }

        [Test]
        public void Correct_status_for_repeated_errors()
        {
            session.Store(new FailedMessage
            {
                Id = "1",
                ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                {
                    new FailedMessage.ProcessingAttempt{AttemptedAt = DateTime.Today},
                    new FailedMessage.ProcessingAttempt{AttemptedAt = DateTime.Today}
                }
            });

         
            session.SaveChanges();

            var message = session.Query<FailedMessage>()
                .TransformWith<MessagesViewTransformer,MessagesView>()
                 .Customize(x => x.WaitForNonStaleResults())
                .Single();

            Assert.AreEqual(MessageStatus.RepeatedFailure, message.Status);
        }


        [SetUp]
        public void SetUp()
        {
            documentStore = InMemoryStoreBuilder.GetInMemoryStore();

            var customIndex = new MessagesViewIndex();
            customIndex.Execute(documentStore);

            var transformer = new MessagesViewTransformer();

            transformer.Execute(documentStore);

            session = documentStore.OpenSession();

        }

        [TearDown]
        public void TearDown()
        {
            session.Dispose();
            documentStore.Dispose();
        }

        IDocumentStore documentStore;
        IDocumentSession session;
    }
}