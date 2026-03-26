namespace ServiceControl.Persistence.Tests.RavenDB.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Persistence.Tests.RavenDB.ObjectExtensions;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Tests.RavenDB;

    [TestFixture]
    class ErrorMessageDataStoreTests : RavenPersistenceTestBase
    {
        IErrorMessageDataStore store;
        FailedMessage processedMessage1, processedMessage2;

        [Test]
        public async Task GetAllMessages()
        {
            var result = await store.GetAllMessages(new PagingInfo(1, 50), new SortInfo("", ""), false);
            Assert.That(result.Results, Is.Not.Empty);
        }

        [Test]
        [TestCase("", "asc", "a")]
        [TestCase("", "dsc", "b")]
        [TestCase("message_id", "asc", "a")]
        [TestCase("message_id", "dsc", "b")]
        [TestCase("critical_time", "asc", "a")]
        [TestCase("critical_time", "dsc", "b")]
        public async Task GetAllMessagesForEndpoint(string sort, string direction, string id)
        {
            var result = await store.GetAllMessagesForEndpoint(
                "RamonAndTomek",
                new PagingInfo(1, 1),
                new SortInfo(sort, direction),
                false
            );

            Assert.That(result.Results, Is.Not.Empty);
            Assert.That(result.Results, Has.Count.EqualTo(1));
            Assert.That(result.Results[0].Id, Is.EqualTo(id));
        }

        [Test]
        public async Task ErrorGet()
        {
            var result = await store.ErrorGet(null, null, null, new PagingInfo(1, 50), new SortInfo("", ""));
            Assert.That(result.Results, Is.Not.Empty);
        }

        [Test]
        public async Task ErrorsSummary_returns_counts_by_status()
        {
            using (var session = DocumentStore.OpenAsyncSession())
            {
                await session.StoreAsync(FailedMessageBuilder.Default(m =>
                {
                    m.Id = "3";
                    m.UniqueMessageId = "c";
                    m.Status = FailedMessageStatus.Archived;
                }));

                await session.StoreAsync(FailedMessageBuilder.Default(m =>
                {
                    m.Id = "4";
                    m.UniqueMessageId = "d";
                    m.Status = FailedMessageStatus.Resolved;
                }));

                await session.StoreAsync(FailedMessageBuilder.Default(m =>
                {
                    m.Id = "5";
                    m.UniqueMessageId = "e";
                    m.Status = FailedMessageStatus.RetryIssued;
                }));

                await session.SaveChangesAsync();
            }

            CompleteDatabaseOperation();

            var result = await store.ErrorsSummary();

            Assert.Multiple(() =>
            {
                Assert.That(result["unresolved"], Is.EqualTo(2));
                Assert.That(result["archived"], Is.EqualTo(1));
                Assert.That(result["resolved"], Is.EqualTo(1));
                Assert.That(result["retryissued"], Is.EqualTo(1));
            });
        }

        [SetUp]
        public async Task GetStore()
        {
            await Task.Yield();
            await GenerateAndSaveFailedMessage();

            CompleteDatabaseOperation();

            store = ServiceProvider.GetRequiredService<IErrorMessageDataStore>();
        }

        async Task GenerateAndSaveFailedMessage()
        {
            using var session = DocumentStore.OpenAsyncSession();
            processedMessage1 = FailedMessageBuilder.Default(m =>
            {
                m.Id = "1";
                m.UniqueMessageId = "a";
                m.ProcessingAttempts.First().MessageMetadata["ReceivingEndpoint"].CastTo<EndpointDetails>().Name = "RamonAndTomek";
                m.ProcessingAttempts.First().MessageMetadata["CriticalTime"] = TimeSpan.FromSeconds(5);
                m.ProcessingAttempts.First().MessageMetadata["TimeSent"] = DateTime.Parse("2023-09-23 00:00:00");
                m.ProcessingAttempts.First().MessageId = "MessageId-1";
            });

            await session.StoreAsync(processedMessage1);

            processedMessage2 = FailedMessageBuilder.Default(m =>
            {
                m.Id = "2";
                m.UniqueMessageId = "b";
                m.ProcessingAttempts.First().MessageMetadata["ReceivingEndpoint"].CastTo<EndpointDetails>().Name = "RamonAndTomek";
                m.ProcessingAttempts.First().MessageMetadata["CriticalTime"] = TimeSpan.FromSeconds(15);
                m.ProcessingAttempts.First().MessageMetadata["TimeSent"] = DateTime.Parse("2023-09-23 00:01:00");
                m.ProcessingAttempts.First().MessageId = "MessageId-2";
            });

            await session.StoreAsync(processedMessage2);

            await session.SaveChangesAsync();
        }
    }
}
