namespace ServiceControl.UnitTests.CompositeViews
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using MessageFailures;
    using MessageFailures.Api;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using PersistenceTests;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Queries;
    using Raven.Client.Documents.Queries.Explanation;
    using Raven.Client.Documents.Queries.Timings;
    using Raven.Client.Documents.Session;
    using Raven.Client.Documents.Session.Tokens;
    using ServiceControl.Operations;

    [TestFixture]
    class FailedMessagesTrialAndError : PersistenceTestBase
    {
        [Test]
        public async Task RqlTransformWorksButUgly()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var selectFunc = DeclareToken.CreateFunction("createResult", @"
                    var lastAttempt = f.ProcessingAttempts[f.ProcessingAttempts.length-1];

                    return {
                        Id: f.UniqueMessageId,
                        MessageType: lastAttempt.MessageMetadata['MessageType'],
                        IsSystemMessage: lastAttempt.MessageMetadata['IsSystemMessage'],
                        SendingEndpoint: lastAttempt.MessageMetadata['SendingEndpoint'],
                        ReceivingEndpoint: lastAttempt.MessageMetadata['ReceivingEndpoint'],
                        TimeSent: lastAttempt.MessageMetadata['TimeSent'],
                        MessageId: lastAttempt.MessageMetadata['MessageId'],
                        Exception: lastAttempt.FailureDetails.Exception,
                        QueueAddress: lastAttempt.FailureDetails.AddressOfFailingEndpoint,
                        NumberOfProcessingAttempts: f.ProcessingAttempts.length,
                        Status: f.Status,
                        TimeOfFailure: lastAttempt.FailureDetails.TimeOfFailure,
                        LastModified: getMetadata(f)['@last-modified'],
                        Edited: !!lastAttempt.Headers['ServiceControl.EditOf'],
                        EditOf: !!lastAttempt.Headers['ServiceControl.EditOf'] ? lastAttempt.Headers['ServiceControl.EditOf'] : ''
                    }
                    ", "f");

                var query = new QueryData(
                    new[] { "createResult(i)" },
                    new List<string>(),
                    "i",
                    new[] { selectFunc },
                    new List<LoadToken>(),
                    true);

                var results = await session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Timings(out QueryTimings timings)
                    .IncludeExplanations(out Explanations explanations)
                    .Statistics(out QueryStatistics stats)
                    .WhereEquals("MessageId", "MessageId")
                    .SelectFields<FailedMessageView>(query)
                    .ToListAsync();

                Assert.IsFalse(stats.IsStale, "Stale results");

                Console.WriteLine(JsonConvert.SerializeObject(results, Formatting.Indented));
            }
        }

        [Test]
        public async Task RqlDeclaresDocQuery()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Statistics(out QueryStatistics stats)
                    .WhereEquals("MessageId", "MessageId")
                    .ToFailedMessageViews()
                    .ToListAsync();

                Assert.IsFalse(stats.IsStale, "Stale results");

                Console.WriteLine(JsonConvert.SerializeObject(results, Formatting.Indented));
            }
        }

        [Test]
        public async Task RqlDeclaresLinqQuery()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Query<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Statistics(out var stats)
                    .Where(sfo => sfo.MessageId == "MessageId")
                    .ToAsyncDocumentQuery()
                    .ToFailedMessageViews()
                    .ToListAsync();

                Assert.IsFalse(stats.IsStale, "Stale results");

                Console.WriteLine(JsonConvert.SerializeObject(results, Formatting.Indented));
            }
        }

        [Test]
        public async Task SelectFields_FailedMessage()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    .Statistics(out QueryStatistics stats)
                    .SelectFields<FailedMessage>()
                    .ToListAsync();

                Assert.IsFalse(stats.IsStale, "Stale results");
            }
        }

        [Test]
        public async Task OfType_FailedMessage()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    //.SetResultTransformer(FailedMessageViewTransformer.Name)
                    .Statistics(out QueryStatistics stats)
                    .OfType<FailedMessage>()
                    .ToListAsync();

                Assert.IsFalse(stats.IsStale, "Stale results");
            }
        }
        [Test]
        public async Task OfType_FailedMessageView()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    //.SetResultTransformer(FailedMessageViewTransformer.Name)
                    .Statistics(out QueryStatistics stats)
                    .OfType<FailedMessageView>()
                    .ToListAsync();

                Assert.IsFalse(stats.IsStale, "Stale results");
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(null, results.First().TimeSent);
            }
        }

        [Test]
        public async Task SelectFields()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    //.SetResultTransformer(FailedMessageViewTransformer.Name)
                    .Statistics(out QueryStatistics stats)
                    .SelectFields<FailedMessageView>()
                    .ToQueryable()
                    .ToListAsync();

                Assert.IsFalse(stats.IsStale, "Stale results");
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(null, results.First().TimeSent);
            }
        }

        [Test]
        public async Task ToQueryable()
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var results = await session.Advanced.AsyncDocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    //.SetResultTransformer(FailedMessageViewTransformer.Name)
                    .Statistics(out QueryStatistics stats)
                    //.SelectFields<FailedMessageView>()
                    .ToQueryable()
                    .ToListAsync();

                Assert.IsFalse(stats.IsStale, "Stale results");
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(null, results.First().TimeSent);
            }
        }

        [Test]
        public async Task Nothing()
        {
            await Task.Yield();

            using (var session = documentStore.OpenSession())
            {
                var results = session.Advanced.DocumentQuery<FailedMessageViewIndex.SortAndFilterOptions, FailedMessageViewIndex>()
                    //.SetResultTransformer(FailedMessageViewTransformer.Name)
                    .Statistics(out QueryStatistics stats)
                    .SelectFields<FailedMessageView>()
                    .ToList();

                Assert.IsFalse(stats.IsStale, "Stale results");
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(null, results.First().TimeSent);
            }
        }

        FailedMessage processedMessage;

        async Task GenerateAndSaveFailedMessage()
        {
            using (var session = documentStore.OpenSession())
            {
                processedMessage = new FailedMessage
                {
                    Id = "MessageId",
                    UniqueMessageId = "UniqueMessageId",
                    Status = FailedMessageStatus.RetryIssued,
                    ProcessingAttempts =
                    {
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.UtcNow,
                            MessageMetadata = new Dictionary<string, object>()
                            {
                                { "MessageType", "MessageType" },
                                { "IsSystemMessage", false },
                                { "SendingEndpoint", new EndpointDetails { Name = "Sending Endpoint", Host = "SendHost", HostId = Guid.NewGuid() } },
                                { "ReceivingEndpoint", new EndpointDetails { Name = "Receiving Endpoint", Host = "RecvHost", HostId = Guid.NewGuid() } },
                                { "TimeSent", DateTime.UtcNow },
                                { "MessageId", "MessageId" },
                            },
                            FailureDetails = new FailureDetails
                            {
                                AddressOfFailingEndpoint = "FailingAddress",
                                TimeOfFailure = DateTime.UtcNow,
                                Exception = new ExceptionDetails
                                {
                                    ExceptionType = "ExceptionType",
                                    Message = "Exception message",
                                    Source = "Exception source",
                                    StackTrace = "Stack trace"
                                }
                            }
                        }
                    }
                };

                session.Store(processedMessage);

                session.SaveChanges();
            }
            await CompleteDatabaseOperation();
        }

        [SetUp]
        public async Task SetupDocumentStore()
        {
            documentStore = GetRequiredService<IDocumentStore>();

            var customIndex = new FailedMessageViewIndex();
            await customIndex.ExecuteAsync(documentStore);

            //var transformer = new FailedMessageViewTransformer();

            //TODO: we need to bring this back
            //transformer.Execute(documentStore);

            await GenerateAndSaveFailedMessage();
        }

        [TearDown]
        public void Dispose()
        {
            documentStore.Dispose();
        }

        IDocumentStore documentStore;
    }

    public static class TestFailedMessageTransformerExtensions
    {
        public static IAsyncDocumentQuery<FailedMessageView> ToFailedMessageViews<T>(this IAsyncDocumentQuery<T> query)
        {
            const string functions = @"
            declare function last(collection) {
                return collection[collection.length-1];
            }

            declare function msgMetadata(msg, key) {
                var lastAttempt = last(msg.ProcessingAttempts);
                return lastAttempt.MessageMetadata[key];
            }

            declare function msgHeader(msg, key) {
                var lastAttempt = last(msg.ProcessingAttempts);
                return lastAttempt.Headers[key];
            }
            ";

            const string rqlTransform = @"
            {
                Id: f.UniqueMessageId,
                MessageType: msgMetadata(f, 'MessageType'),
                IsSystemMessage: msgMetadata(f, 'IsSystemMessage'),
                SendingEndpoint: msgMetadata(f, 'SendingEndpoint'),
                ReceivingEndpoint: msgMetadata(f, 'ReceivingEndpoint'),
                TimeSent: msgMetadata(f, 'TimeSent'),
                MessageId: msgMetadata(f, 'MessageId'),
                Exception: last(f.ProcessingAttempts).FailureDetails.Exception,
                QueueAddress: last(f.ProcessingAttempts).FailureDetails.AddressOfFailingEndpoint,
                NumberOfProcessingAttempts: f.ProcessingAttempts.length,
                Status: f.Status,
                TimeOfFailure: last(f.ProcessingAttempts).FailureDetails.TimeOfFailure,
                LastModified: getMetadata(f)['@last-modified'],
                Edited: !!msgHeader(f, 'ServiceControl.EditOf'),
                EditOf: !!msgHeader(f, 'ServiceControl.EditOf') ? msgHeader(f, 'ServiceControl.EditOf') : ''
            }
            ";

            var queryData = QueryData.CustomFunction("f", rqlTransform);
            // I gather this wouldn't be necessary given Default is to look at Index then Document,
            // but since we know it's all in the document, this seems like a good optimization?
            queryData.ProjectionBehavior = ProjectionBehavior.FromDocument;

            return query
                .BeforeQueryExecuted(q =>
                {
                    q.Query = functions + q.Query;
                    Console.WriteLine(q.Query);
                })
                .SelectFields<FailedMessageView>(queryData);
        }

        public static IAsyncDocumentQuery<FailedMessageView> ToFailedMessageViews<T>(this IQueryable<T> query)
        {
            return query.ToAsyncDocumentQuery().ToFailedMessageViews();
        }
    }
}