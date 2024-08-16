namespace ServiceControl.Persistence.Tests.RavenDB.CompositeViews
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MessageAuditing;
    using MessageFailures;
    using NUnit.Framework;
    using Persistence.Tests.RavenDB;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Persistence;

    [TestFixture]
    class MessagesViewTests : RavenPersistenceTestBase
    {
        [Test]
        public void Filter_out_system_messages()
        {
            using (var session = DocumentStore.OpenSession())
            {
                var processedMessage = FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "1";
                    m.ProcessingAttempts.First().MessageMetadata["IsSystemMessage"] = true;
                });

                session.Store(processedMessage);

                var processedMessage2 = FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "2";
                    m.ProcessingAttempts.First().MessageMetadata["IsSystemMessage"] = false;
                });

                session.Store(processedMessage2);

                session.SaveChanges();
            }

            DocumentStore.WaitForIndexing();

            using (var session = DocumentStore.OpenSession())
            {
                var results = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .Where(x => !x.IsSystemMessage)
                    .OfType<ProcessedMessage>()
                    .ToList();
                Assert.That(results.Count, Is.EqualTo(1));
                Assert.That(results.Single().Id, Is.Not.EqualTo("1"));
            }
        }

        [Test]
        public void Order_by_critical_time()
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Store(FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "1";
                    m.ProcessingAttempts.First().MessageMetadata["CriticalTime"] = TimeSpan.FromSeconds(10);
                }));

                session.Store(FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "2";
                    m.ProcessingAttempts.First().MessageMetadata["CriticalTime"] = TimeSpan.FromSeconds(20);
                }));

                session.Store(FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "3";
                    m.ProcessingAttempts.First().MessageMetadata["CriticalTime"] = TimeSpan.FromSeconds(15);
                }));

                session.Store(FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "4";
                    m.Status = FailedMessageStatus.Unresolved;
                    m.ProcessingAttempts.First().MessageMetadata["CriticalTime"] = TimeSpan.FromSeconds(15);
                }));

                session.SaveChanges();
            }

            DocumentStore.WaitForIndexing();

            using (var session = DocumentStore.OpenSession())
            {
                var firstByCriticalTime = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderBy(x => x.CriticalTime)
                    .Where(x => x.CriticalTime.HasValue)
                    .ProjectInto<ProcessedMessage>() //https://ravendb.net/docs/article-page/4.2/csharp/migration/client-api/session/querying/basics#projectfromindexfieldsinto
                    .First();

                Assert.That(firstByCriticalTime.Id, Is.EqualTo("1"));

                var firstByCriticalTimeDescription = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderByDescending(x => x.CriticalTime)
                    .Where(x => x.CriticalTime.HasValue)
                    .ProjectInto<ProcessedMessage>()
                    .First();
                Assert.That(firstByCriticalTimeDescription.Id, Is.EqualTo("2"));
            }
        }

        [Test]
        public void Order_by_time_sent()
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Store(FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "1";
                    m.ProcessingAttempts.First().MessageMetadata["TimeSent"] = DateTime.Today.AddSeconds(20);
                }));

                session.Store(FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "2";
                    m.ProcessingAttempts.First().MessageMetadata["TimeSent"] = DateTime.Today.AddSeconds(10);
                }));

                session.Store(FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "3";
                    m.ProcessingAttempts.First().MessageMetadata["TimeSent"] = DateTime.Today.AddDays(-1);
                }));

                session.SaveChanges();
            }

            DocumentStore.WaitForIndexing();

            using (var session = DocumentStore.OpenSession())
            {
                var firstByTimeSent = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderBy(x => x.TimeSent)
                    .ProjectInto<ProcessedMessage>()
                    .First();
                Assert.That(firstByTimeSent.Id, Is.EqualTo("3"));

                var firstByTimeSentDescription = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .OrderByDescending(x => x.TimeSent)
                    .ProjectInto<ProcessedMessage>()
                    .First();
                Assert.That(firstByTimeSentDescription.Id, Is.EqualTo("1"));
            }
        }

        [Test]
        public async Task TimeSent_is_not_cast_to_DateTimeMin_if_null()
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Store(FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "1";
                    m.ProcessingAttempts.First().MessageMetadata["TimeSent"] = null;
                }));

                session.Store(FailedMessageBuilder.Minimal(m =>
                {
                    m.Id = "2";

                    m.ProcessingAttempts.First().AttemptedAt = DateTime.Today;
                    m.ProcessingAttempts.First().MessageMetadata["TimeSent"] = null;

                    m.ProcessingAttempts.Add(new FailedMessage.ProcessingAttempt
                    {
                        AttemptedAt = DateTime.Today,
                        MessageMetadata =
                        {
                            ["MessageIntent"] = "Send",
                            ["TimeSent"] = null
                        }
                    });
                }));

                session.SaveChanges();
            }

            DocumentStore.WaitForIndexing();

            using (var session = DocumentStore.OpenAsyncSession())
            {
                var query = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .OfType<FailedMessage>();

                var messagesWithNoTimestamp = await query.TransformToMessageView().ToArrayAsync();

                Assert.That(messagesWithNoTimestamp[0].TimeSent, Is.EqualTo(null));
                Assert.That(messagesWithNoTimestamp[1].TimeSent, Is.EqualTo(null));
            }
        }

        [TestCase(FailedMessageStatus.Archived, MessageStatus.ArchivedFailure)]
        [TestCase(FailedMessageStatus.Resolved, MessageStatus.ResolvedSuccessfully)]
        [TestCase(FailedMessageStatus.RetryIssued, MessageStatus.RetryIssued)]
        [TestCase(FailedMessageStatus.Unresolved, MessageStatus.Failed)]
        public async Task Correct_status_for_failed_messages(FailedMessageStatus failedMessageStatus, MessageStatus expecteMessageStatus)
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Store(new FailedMessage
                {
                    Id = "1",
                    ProcessingAttempts =
                    [
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.Today,
                            MessageMetadata =
                            {
                                ["MessageIntent"] = "Send",
                                ["CriticalTime"] = TimeSpan.FromSeconds(1)
                            }
                        }
                    ],
                    Status = failedMessageStatus
                });

                session.SaveChanges();
            }

            DocumentStore.WaitForIndexing();

            using (var session = DocumentStore.OpenAsyncSession())
            {
                var query = session.Query<FailedMessage>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .OfType<FailedMessage>();

                var result = await query.TransformToMessageView().ToListAsync();

                var message = result.Single();

                Assert.That(message.Status, Is.EqualTo(expecteMessageStatus));
            }
        }

        [Test]
        public async Task Correct_status_for_repeated_errors()
        {
            using (var session = DocumentStore.OpenSession())
            {
                session.Store(new FailedMessage
                {
                    Id = "1",
                    ProcessingAttempts =
                    [
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.Today,
                            MessageMetadata = new Dictionary<string, object> { { "MessageIntent", "1" } }
                        },
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.Today,
                            MessageMetadata = new Dictionary<string, object> { { "MessageIntent", "1" } }
                        }
                    ]
                });

                session.SaveChanges();
            }

            DocumentStore.WaitForIndexing();

            using (var session = DocumentStore.OpenAsyncSession())
            {
                var query = session
                    .Query<FailedMessage>()
                    .Customize(x => x.WaitForNonStaleResults())
                    .OfType<FailedMessage>();

                var result = await query.TransformToMessageView().ToListAsync();

                var message = result.Single();

                Assert.That(message.Status, Is.EqualTo(MessageStatus.RepeatedFailure));
            }
        }
    }
}