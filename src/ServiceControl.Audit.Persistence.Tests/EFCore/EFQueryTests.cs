namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Monitoring;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.EntityConfigurations;

    class EFQueryTests : EFPersistenceTestFixture
    {
        [TestCaseSource(nameof(TimeRangeRoutes))]
        public async Task Filters_on_a_time_range_given_with_an_offset(Func<IAuditMessagesViewDataStore, DateTimeRange, CancellationToken, Task<QueryResult<IList<MessagesView>>>> route)
        {
            await Ingest(MakeMessage(extraHeaders: new Dictionary<string, string> { [SearchableHeader] = SearchableWord }));

            var inRange = await route(MessagesViewStore, new DateTimeRange("2026-09-01T12:00:00+02:00", "2026-09-01T12:45:00+02:00"), TestTimeoutCancellationToken);
            var outOfRange = await route(MessagesViewStore, new DateTimeRange("2026-09-01T12:45:00+02:00"), TestTimeoutCancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(inRange.Results, Has.Count.EqualTo(1));
                Assert.That(outOfRange.Results, Is.Empty);
            }
        }

        [TestCaseSource(nameof(TimeRangeRoutes))]
        public async Task Filters_on_a_time_range_given_without_a_zone(Func<IAuditMessagesViewDataStore, DateTimeRange, CancellationToken, Task<QueryResult<IList<MessagesView>>>> route)
        {
            await Ingest(MakeMessage(extraHeaders: new Dictionary<string, string> { [SearchableHeader] = SearchableWord }));

            var inRange = await route(MessagesViewStore, new DateTimeRange("2026-09-01T10:15:00", "2026-09-01T10:45:00"), TestTimeoutCancellationToken);
            var outOfRange = await route(MessagesViewStore, new DateTimeRange("2026-09-01T10:45:00"), TestTimeoutCancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(inRange.Results, Has.Count.EqualTo(1));
                Assert.That(outOfRange.Results, Is.Empty);
            }
        }

        static IEnumerable<TestCaseData> TimeRangeRoutes()
        {
            yield return Route(nameof(IAuditMessagesViewDataStore.GetMessages),
                (store, range, token) => store.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"), range, token));
            yield return Route(nameof(IAuditMessagesViewDataStore.QueryMessages),
                (store, range, token) => store.QueryMessages(SearchableWord, new PagingInfo(), new SortInfo("time_sent", "desc"), range, token));
            yield return Route(nameof(IAuditMessagesViewDataStore.QueryMessagesByReceivingEndpointAndKeyword),
                (store, range, token) => store.QueryMessagesByReceivingEndpointAndKeyword("Receiver", SearchableWord, new PagingInfo(), new SortInfo("time_sent", "desc"), range, token));
            yield return Route(nameof(IAuditMessagesViewDataStore.QueryMessagesByReceivingEndpoint),
                (store, range, token) => store.QueryMessagesByReceivingEndpoint(true, "Receiver", new PagingInfo(), new SortInfo("time_sent", "desc"), range, token));
        }

        static TestCaseData Route(string name, Func<IAuditMessagesViewDataStore, DateTimeRange, CancellationToken, Task<QueryResult<IList<MessagesView>>>> route) =>
            new TestCaseData(route).SetArgDisplayNames(name);

        const string SearchableHeader = "Order.Reference";
        const string SearchableWord = "meridian";

        [Test]
        public async Task Finds_a_header_value_that_is_not_ascii()
        {
            await Ingest(MakeMessage(extraHeaders: new Dictionary<string, string> { ["Shipping.Step"] = "Bestellprüfung" }));

            var found = await MessagesViewStore.QueryMessages("Bestellprüfung", new PagingInfo(), new SortInfo("time_sent", "desc"));

            Assert.That(found.Results, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Finds_a_word_that_is_quoted_in_single_quotes()
        {
            await Ingest(MakeMessage(extraHeaders: new Dictionary<string, string> { ["Faults.Message"] = "The given key 'CustomerId' was not present in the dictionary." }));

            var found = await MessagesViewStore.QueryMessages("CustomerId", new PagingInfo(), new SortInfo("time_sent", "desc"));

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

            Assert.That(ColumnLengths.FitToIndex(value), Does.StartWith(new string('x', 384) + "~"));
        }

        [Test]
        public async Task Reports_an_over_length_conversation_id_that_needs_no_url_encoding()
        {
            await Ingest(MakeMessage(conversationId: new string('c', 600)));

            var view = (await MessagesViewStore.GetMessages(true, new PagingInfo(), new SortInfo("time_sent", "desc"))).Results.Single();

            Assert.That(Uri.EscapeDataString(view.ConversationId), Is.EqualTo(view.ConversationId), "ServicePulse puts the conversation id into a URL path without encoding it");
        }
    }
}
