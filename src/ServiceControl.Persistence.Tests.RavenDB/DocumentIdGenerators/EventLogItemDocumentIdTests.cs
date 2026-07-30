namespace ServiceControl.Persistence.Tests.RavenDB.DocumentIdGenerators
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.EventLog;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Persistence.RavenDB;

    [TestFixture]
    class EventLogItemDocumentIdTests : RavenPersistenceTestBase
    {
        [Test]
        public async Task Returned_id_is_a_document_id_for_the_items_category_and_event_type()
        {
            await EventLogDataStore.Add(Item("Message processing failed"));
            await CompleteDatabaseOperation();

            var items = (await EventLogDataStore.GetEventLogItems(new PagingInfo())).Results;

            var id = items[0].Id;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(id, Does.StartWith("EventLogItem/Recoverability/MessageFailed/"));
                Assert.That(
                    EventLogItemIdGenerator.GetEventIdFromDocumentId(id),
                    Is.Not.EqualTo(Guid.Empty),
                    "the final segment must be the minted event id, not an empty or absent GUID");
            }
        }

        // Category and EventType are identical here, so the minted GUID is the only thing keeping the
        // two document ids apart. Were it dropped or made deterministic, the second write would
        // silently replace the first rather than adding to the feed.
        [Test]
        public async Task Two_items_of_the_same_kind_do_not_overwrite_each_other()
        {
            await EventLogDataStore.Add(Item("first"));
            await EventLogDataStore.Add(Item("second"));
            await CompleteDatabaseOperation();

            var result = await EventLogDataStore.GetEventLogItems(new PagingInfo());

            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(2));
        }

        static EventLogItem Item(string description) => new()
        {
            Category = "Recoverability",
            EventType = "MessageFailed",
            Description = description,
            Severity = Severity.Error,
            RaisedAt = new DateTime(2026, 7, 22, 10, 30, 0, DateTimeKind.Utc),
            RelatedTo = []
        };
    }
}
