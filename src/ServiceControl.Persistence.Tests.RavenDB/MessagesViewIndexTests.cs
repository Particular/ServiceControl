namespace ServiceControl.Persistence.Tests.RavenDB
{
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Operations.Indexes;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence;

    [TestFixture]
    class MessagesViewIndexTests : RavenPersistenceTestBase
    {
        [Test]
        public async Task Should_map_failed_messages_without_errors_and_find_them_by_header_value()
        {
            var failedMessage = await SeedFailedMessage(FailedMessageBuilder.Default(m => m.ProcessingAttempts[0].Headers["HeaderA"] = "Gandalf"));
            await DocumentStore.WaitForIndexingAsync();

            var indexErrors = await DocumentStore.Maintenance.SendAsync(new GetIndexErrorsOperation([nameof(MessagesViewIndex)]));

            using var session = DocumentStore.OpenAsyncSession();
            var results = await session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>()
                .Search(x => x.Query, "Gandalf")
                .OfType<FailedMessage>()
                .ToListAsync();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(indexErrors.Single().Errors, Is.Empty);
                Assert.That(results.Select(x => x.Id), Is.EqualTo(new[] { failedMessage.Id }));
            }
        }
    }
}
