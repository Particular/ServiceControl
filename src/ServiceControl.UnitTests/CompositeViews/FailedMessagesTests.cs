namespace ServiceControl.UnitTests.CompositeViews
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Contracts.Operations;
    using MessageFailures;
    using MessageFailures.Api;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;
    using Raven.TestDriver;

    [TestFixture]
    public class FailedMessagesTests : RavenTestDriver
    {
        [Test]
        public void Should_allow_errors_with_no_metadata()
        {
            using (var session = documentStore.OpenSession())
            {
                var processedMessage = new FailedMessage
                {
                    Id = "1",
                    UniqueMessageId = "xyz",
                    Status = FailedMessageStatus.Unresolved,
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.UtcNow,
                            MessageMetadata = new Dictionary<string, object>(),
                            FailureDetails = new FailureDetails()
                        }
                    }
                };

                session.Store(processedMessage);

                session.SaveChanges();
            }

            QueryStatistics stats;

            do
            {
                using (var session = documentStore.OpenSession())
                {
                    var results = session.Advanced.DocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                        .Statistics(out stats)
                        .SelectFields<FailedMessageView>()
                        .ToList();


                    if (!stats.IsStale)
                    {
                        Console.Out.WriteLine("Checking result");
                        Assert.AreEqual(1, results.Count);

                        Assert.AreEqual(null, results.First().TimeSent);
                    }
                }


                if (stats.IsStale)
                {
                    Thread.Sleep(1000);
                }
            } 
            while (stats.IsStale);
        }


        [SetUp]
        public void SetUp()
        {
            documentStore = GetDocumentStore();

            var customIndex = new FailedMessageViewIndex();
            customIndex.Execute(documentStore);
        }

        [TearDown]
        public void TearDown()
        {
            documentStore.Dispose();
        }

        IDocumentStore documentStore;
    }
}