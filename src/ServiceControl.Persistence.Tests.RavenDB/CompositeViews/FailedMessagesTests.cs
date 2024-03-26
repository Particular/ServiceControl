namespace ServiceControl.UnitTests.CompositeViews
{
    using System;
    using System.Linq;
    using System.Threading;
    using Contracts.Operations;
    using MessageFailures;
    using MessageFailures.Api;
    using NUnit.Framework;
    using Raven.Client.Documents.Session;

    [TestFixture]
    class FailedMessagesTests : PersistenceTestBase
    {
        [Test]
        public void Should_allow_errors_with_no_metadata()
        {
            using (var session = DocumentStore.OpenSession())
            {
                var processedMessage = new FailedMessage
                {
                    Id = "1",
                    UniqueMessageId = "xyz",
                    Status = FailedMessageStatus.Unresolved,
                    ProcessingAttempts =
                    [
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.UtcNow,
                            MessageMetadata = [],
                            FailureDetails = new FailureDetails()
                        }
                    ]
                };

                session.Store(processedMessage);

                session.SaveChanges();
            }

            QueryStatistics stats;

            do
            {
                using (var session = DocumentStore.OpenSession())
                {
                    var results = session.Advanced.DocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                        //.SetResultTransformer(FailedMessageViewTransformer.Name)
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
    }
}