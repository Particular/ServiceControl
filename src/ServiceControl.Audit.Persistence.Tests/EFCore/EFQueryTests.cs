namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Monitoring;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.EntityConfigurations;

    class EFQueryTests : EFPersistenceTestFixture
    {
        [Test]
        public async Task Filters_on_a_time_range_given_with_an_offset()
        {
            await Ingest(MakeMessage());

            var inRange = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"), new DateTimeRange("2026-09-01T12:00:00+02:00", "2026-09-01T12:45:00+02:00"));
            var outOfRange = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"), new DateTimeRange("2026-09-01T12:45:00+02:00"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(inRange.Results, Has.Count.EqualTo(1));
                Assert.That(outOfRange.Results, Is.Empty);
            }
        }

        [Test]
        public async Task Finds_a_header_value_that_is_not_ascii()
        {
            await Ingest(MakeMessage(extraHeaders: new Dictionary<string, string> { ["Shipping.Step"] = "Bestellprüfung" }));

            var found = await MessagesViewStore.QueryMessages("Bestellprüfung", new PagingInfo(), new SortInfo("time_sent", "desc"));

            Assert.That(found.Results, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Sorts_by_status_in_the_order_the_primary_merges_instance_pages()
        {
            await Ingest(MakeMessage(isRetried: true), MakeMessage());

            var ascending = await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("status", "asc"));

            Assert.That(ascending.Results.Select(message => message.Status), Is.EqualTo(new[] { MessageStatus.Successful, MessageStatus.ResolvedSuccessfully }));
        }

        [Test]
        public async Task Stores_a_message_id_of_any_length()
        {
            var messageId = new string('m', 600);

            await Ingest(MakeMessage(messageId: messageId));

            var view = (await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"))).Results.Single();

            Assert.That(view.MessageId, Is.EqualTo(messageId));
        }

        [Test]
        public async Task Finds_a_conversation_whose_id_is_longer_than_an_index_allows()
        {
            var conversationId = new string('c', 600);

            await Ingest(MakeMessage(conversationId: conversationId));

            var conversation = await MessagesViewStore.QueryMessagesByConversationId(conversationId, new PagingInfo(), new SortInfo("time_sent", "desc"));

            Assert.That(conversation.Results, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Keeps_apart_conversations_whose_ids_differ_only_past_the_index_length()
        {
            var sharedStart = new string('c', ColumnLengths.ShortTextLength);

            await Ingest(
                MakeMessage(messageId: "first", conversationId: sharedStart + "-first"),
                MakeMessage(messageId: "second", conversationId: sharedStart + "-second"));

            var conversation = await MessagesViewStore.QueryMessagesByConversationId(sharedStart + "-first", new PagingInfo(), new SortInfo("time_sent", "desc"));

            Assert.That(conversation.Results.Select(view => view.MessageId), Is.EqualTo(new[] { "first" }));
        }

        [Test]
        public void Fits_a_long_value_to_the_index_without_splitting_a_surrogate_pair()
        {
            var value = new string('x', 384) + "😀" + new string('y', 100);

            Assert.That(ColumnLengths.FitToIndex(value), Does.StartWith(new string('x', 384) + "#"));
        }
    }
}
