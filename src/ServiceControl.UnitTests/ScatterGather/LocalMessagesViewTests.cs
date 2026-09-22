namespace ServiceControl.UnitTests.ScatterGather
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;

    [TestFixture]
    public class LocalMessagesViewTests
    {
        [Test]
        public void A_message_that_both_failed_and_was_audited_shows_as_failed()
        {
            var failed = Message("Receiver", "1", MessageStatus.Failed);
            var audited = Message("Receiver", "1", MessageStatus.Successful);

            var result = LocalMessagesView.Merge([failed], [audited], new PagingInfo());

            Assert.That(result.Results.Single().Status, Is.EqualTo(MessageStatus.Failed));
        }

        [Test]
        public void A_message_that_both_failed_and_was_audited_is_counted_once()
        {
            var failed = Message("Receiver", "1", MessageStatus.Failed);
            var audited = Message("Receiver", "1", MessageStatus.Successful);

            var result = LocalMessagesView.Merge([failed], [audited], new PagingInfo());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Results, Has.Count.EqualTo(1));
                Assert.That(result.QueryStats.TotalCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void The_same_message_id_on_a_different_endpoint_is_a_different_message()
        {
            var failed = Message("Receiver", "1", MessageStatus.Failed);
            var audited = Message("OtherReceiver", "1", MessageStatus.Successful);

            var result = LocalMessagesView.Merge([failed], [audited], new PagingInfo());

            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(2));
        }

        [Test]
        public void A_full_page_from_each_source_is_truncated_to_one_page()
        {
            var pagingInfo = new PagingInfo(pageSize: 5);
            var failed = Enumerable.Range(0, 5).Select(i => Message("Receiver", $"failed-{i}", MessageStatus.Failed)).ToArray();
            var audited = Enumerable.Range(0, 5).Select(i => Message("Receiver", $"audited-{i}", MessageStatus.Successful)).ToArray();

            var result = LocalMessagesView.Merge(failed, audited, pagingInfo);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Results, Has.Count.EqualTo(5), "the scatter gather truncates to a page, so the page must already be the local answer");
                Assert.That(result.QueryStats.TotalCount, Is.EqualTo(10), "the total counts every distinct message, not just the page");
            }
        }

        [Test]
        public void The_page_is_taken_after_the_requested_order_is_applied()
        {
            var pagingInfo = new PagingInfo(pageSize: 2);
            var failed = new[] { Message("Receiver", "c", MessageStatus.Failed), Message("Receiver", "a", MessageStatus.Failed) };
            var audited = new[] { Message("Receiver", "b", MessageStatus.Successful) };

            var result = LocalMessagesView.Merge(failed, audited, pagingInfo, MessageViewComparer.FromSortInfo(new SortInfo("message_id", "asc")));

            Assert.That(result.Results.Select(message => message.MessageId), Is.EqualTo(new[] { "a", "b" }).AsCollection);
        }

        [Test]
        public void The_local_key_matches_the_key_the_scatter_gather_deduplicates_on()
        {
            var message = Message("Receiver", "1", MessageStatus.Failed);

            Assert.That(LocalMessagesView.DeduplicationKey(message), Is.EqualTo("Receiver-1"));
        }

        static MessagesView Message(string receivingEndpoint, string messageId, MessageStatus status) => new()
        {
            MessageId = messageId,
            Status = status,
            ReceivingEndpoint = new EndpointDetails { Name = receivingEndpoint, Host = "host", HostId = Guid.NewGuid() }
        };
    }
}
