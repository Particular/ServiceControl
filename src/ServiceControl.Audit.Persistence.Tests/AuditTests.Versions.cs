namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Auditing;
    using Auditing.MessagesView;
    using Monitoring;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    partial class AuditTests
    {
        [Test]
        public async Task A_list_version_moves_when_a_matching_message_arrives([Values] ListQuery query)
        {
            var conversationId = Guid.NewGuid().ToString();
            await IngestProcessedMessagesAudits(MakeReceivedMessage(conversationId));

            var issued = await List(query, conversationId);
            var repeated = await List(query, conversationId);

            await IngestProcessedMessagesAudits(MakeReceivedMessage(conversationId));

            var afterArrival = await List(query, conversationId);

            Assert.That(afterArrival.Results, Has.Count.EqualTo(2), $"the {query} query did not see the new message");
            VersionAssert.Matches(issued.QueryStats.Version, repeated.QueryStats.Version, "nothing was written between the two reads");
            VersionAssert.Moved(repeated.QueryStats.Version, afterArrival.QueryStats.Version, "a client holding the old version would never see the new message");
        }

        [Test]
        public async Task A_list_version_moves_when_the_total_moves_under_an_unchanged_page()
        {
            var onePage = new PagingInfo(page: 1, pageSize: 1);
            var byMessageId = new SortInfo("message_id", "asc");

            await IngestProcessedMessagesAudits(MakeMessage("b"));
            var before = await MessagesViewStore.GetMessages(false, onePage, byMessageId, cancellationToken: TestContext.CurrentContext.CancellationToken);

            await IngestProcessedMessagesAudits(MakeMessage("c"));
            var after = await MessagesViewStore.GetMessages(false, onePage, byMessageId, cancellationToken: TestContext.CurrentContext.CancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(after.Results.Select(message => message.MessageId), Is.EqualTo(new[] { "b" }), "the page itself was meant to stay the same");
                Assert.That(after.QueryStats.TotalCount, Is.EqualTo(2));
            }

            VersionAssert.Moved(before.QueryStats.Version, after.QueryStats.Version, "Total-Count went from one to two, so a client holding the old version would keep showing one");
        }

        public enum ListQuery
        {
            All,
            Search,
            Endpoint,
            EndpointAndKeyword,
            Conversation
        }

        ProcessedMessage MakeReceivedMessage(string conversationId)
        {
            var message = MakeMessage(conversationId: conversationId);
            message.MessageMetadata["ReceivingEndpoint"] = new EndpointDetails { Name = ReceivingEndpoint };
            return message;
        }

        async Task<QueryResult<IList<MessagesView>>> List(ListQuery query, string conversationId)
        {
            var paging = new PagingInfo();
            var sort = new SortInfo("time_sent", "desc");
            var cancellationToken = TestContext.CurrentContext.CancellationToken;

            var result = query switch
            {
                ListQuery.All => await MessagesViewStore.GetMessages(false, paging, sort, cancellationToken: cancellationToken),
                ListQuery.Search => await MessagesViewStore.QueryMessages("MyMessageType", paging, sort, cancellationToken: cancellationToken),
                ListQuery.Endpoint => await MessagesViewStore.QueryMessagesByReceivingEndpoint(false, ReceivingEndpoint, paging, sort, cancellationToken: cancellationToken),
                ListQuery.EndpointAndKeyword => await MessagesViewStore.QueryMessagesByReceivingEndpointAndKeyword(ReceivingEndpoint, "MyMessageType", paging, sort, cancellationToken: cancellationToken),
                ListQuery.Conversation => await MessagesViewStore.QueryMessagesByConversationId(conversationId, paging, sort, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(query), query, null)
            };

            Assert.That(result.Results, Is.Not.Empty, $"the {query} query found nothing, so its version says nothing about the message");

            return result;
        }

        const string ReceivingEndpoint = "SomeEndpoint";
    }
}
