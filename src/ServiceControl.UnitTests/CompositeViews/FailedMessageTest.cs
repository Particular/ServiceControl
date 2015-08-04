namespace ServiceControl.UnitTests.CompositeViews
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using MessageFailures;
    using NUnit.Framework;
    using Raven.Client;
    using ServiceControl.MessageFailures.Api;

    [TestFixture]
    public class FailedMessagesTests 
    {
        [Test]
        public void Should_not_provide_defaults_for_missing_metadata()
        {
            using (var session = documentStore.OpenSession())
            {
                var processedMessage = new FailedMessage
                                       {
                                           Id = "1",
                                           UniqueMessageId = "xyz",
                                           Status = FailedMessageStatus.Unresolved,
                                           ProcessingAttempts = new List<FailedMessage.ProcessingAttempt> { new FailedMessage.ProcessingAttempt
                                           {
                                               AttemptedAt = DateTime.UtcNow,
                                               MessageMetadata = new Dictionary<string, object>()
                                           }}
                                       };

                session.Store(processedMessage);

                session.SaveChanges();
            }

            RavenQueryStatistics stats;

            do
            {
                using (var session = documentStore.OpenSession())
                {


                    var results = session.Advanced.LuceneQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                                        .SetResultTransformer(new FailedMessageViewTransformer().TransformerName)
                                        .Statistics(out stats)
                                            .SelectFields<FailedMessageView>()
                                        .ToList();


                    if (!stats.IsStale)
                    {
                        Console.Out.WriteLine("Checking result");
                        Assert.AreEqual(1, results.Count);

                        Assert.AreEqual(null, results.First().TimeSent);
                        Assert.AreEqual(null, results.First().MessageType);
                        Assert.AreEqual(null, results.First().ReceivingEndpoint);
                        Assert.AreEqual(null, results.First().SendingEndpoint);
                    }
                }



                if (stats.IsStale)
                    Thread.Sleep(1000);

            } while (stats.IsStale);


        }


        [SetUp]
        public void SetUp()
        {
            documentStore = InMemoryStoreBuilder.GetInMemoryStore();

            var customIndex = new FailedMessageViewIndex();
            customIndex.Execute(documentStore);

            var transformer = new FailedMessageViewTransformer();

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