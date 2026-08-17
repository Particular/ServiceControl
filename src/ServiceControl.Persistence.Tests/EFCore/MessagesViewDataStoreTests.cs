namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NUnit.Framework;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;

class MessagesViewDataStoreTests : ErrorIngestionTestBase
{
    [Test]
    public async Task Reports_a_stored_failure()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);

        var view = await SingleMessage();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.EqualTo(failure.UniqueMessageIdString));
            Assert.That(view.MessageId, Is.EqualTo(failure.MessageId));
            Assert.That(view.MessageType, Is.EqualTo(failure.MessageType));
            Assert.That(view.ConversationId, Is.EqualTo(failure.ConversationId));
            Assert.That(view.TimeSent, Is.EqualTo(failure.TimeSent));
            Assert.That(view.ProcessedAt, Is.EqualTo(failure.AttemptedAt));
            Assert.That(view.SendingEndpoint.Name, Is.EqualTo(failure.SendingEndpoint.Name));
            Assert.That(view.ReceivingEndpoint.Name, Is.EqualTo(failure.ReceivingEndpoint.Name));
            Assert.That(view.BodySize, Is.EqualTo(failure.Body.Length));
            Assert.That(view.BodyUrl, Is.EqualTo($"/messages/{failure.UniqueMessageIdString}/body"));
            Assert.That(view.IsSystemMessage, Is.False);
            Assert.That(view.Headers.ToDictionary(header => header.Key, header => header.Value),
                Does.ContainKey(NServiceBus.Headers.EnclosedMessageTypes));
        }
    }

    [Test]
    public async Task Reports_the_message_intent()
    {
        await Ingest(new IngestedFailure { MessageIntent = MessageIntent.Reply });

        var view = await SingleMessage();

        Assert.That(view.MessageIntent, Is.EqualTo(MessageIntent.Reply));
    }

    /// <summary>
    /// The error instance never enriches the processing statistics, so RavenDB reports zeroes here
    /// too. The sort options for them fall through to time_sent for the same reason.
    /// </summary>
    [Test]
    public async Task Reports_no_processing_statistics()
    {
        await Ingest(new IngestedFailure());

        var view = await SingleMessage();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.CriticalTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(view.ProcessingTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(view.DeliveryTime, Is.EqualTo(TimeSpan.Zero));
        }
    }

    [Test]
    public async Task Reports_a_null_time_sent()
    {
        await Ingest(new IngestedFailure { TimeSent = null });

        var view = await SingleMessage();

        Assert.That(view.TimeSent, Is.Null);
    }

    [TestCase(FailedMessageStatus.Unresolved, 1, MessageStatus.Failed)]
    [TestCase(FailedMessageStatus.Unresolved, 2, MessageStatus.RepeatedFailure)]
    [TestCase(FailedMessageStatus.Resolved, 1, MessageStatus.ResolvedSuccessfully)]
    [TestCase(FailedMessageStatus.RetryIssued, 1, MessageStatus.RetryIssued)]
    [TestCase(FailedMessageStatus.Archived, 1, MessageStatus.ArchivedFailure)]
    [TestCase(FailedMessageStatus.Archived, 2, MessageStatus.ArchivedFailure)]
    public async Task Reports_the_status(FailedMessageStatus status, int attempts, MessageStatus expected)
    {
        await Insert(new IngestedFailure(), status, attempts);

        var view = await SingleMessage();

        Assert.That(view.Status, Is.EqualTo(expected));
    }

    [Test]
    public async Task Hides_system_messages_unless_asked()
    {
        var system = new IngestedFailure { IsSystemMessage = true };

        await Ingest(new IngestedFailure(), system);

        var hidden = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), includeSystemMessages: false);
        var included = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), includeSystemMessages: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hidden.Results.Select(view => view.Id), Does.Not.Contain(system.UniqueMessageIdString));
            Assert.That(included.Results.Select(view => view.Id), Does.Contain(system.UniqueMessageIdString));
        }
    }

    [Test]
    public async Task Sorts_by_time_sent_by_default()
    {
        var (oldest, middle, newest) = await IngestThreeSentMinutesApart();

        var descending = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), true);
        var ascending = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(direction: "asc"), true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(descending.Results.Select(view => view.Id), Is.EqualTo(new[] { newest, middle, oldest }));
            Assert.That(ascending.Results.Select(view => view.Id), Is.EqualTo(new[] { oldest, middle, newest }));
        }
    }

    /// <summary>
    /// The error instance reports zero for all three, so they sort by time sent like everything the
    /// API does not sort by.
    /// </summary>
    [TestCase("critical_time")]
    [TestCase("delivery_time")]
    [TestCase("processing_time")]
    public async Task Sorts_by_time_sent_for_the_statistics_options(string sort)
    {
        var (oldest, middle, newest) = await IngestThreeSentMinutesApart();

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(sort), true);

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { newest, middle, oldest }));
    }

    [Test]
    public async Task Sorts_by_status_as_reported()
    {
        var failed = new IngestedFailure();
        var archived = new IngestedFailure();

        await Insert(failed, FailedMessageStatus.Unresolved, 1);
        await Insert(archived, FailedMessageStatus.Archived, 1);

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo("status", "asc"), true);

        // Failed is 1 and ArchivedFailure is 5 as MessageStatus, the reverse of the order the
        // FailedMessageStatus column stores them in.
        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { failed.UniqueMessageIdString, archived.UniqueMessageIdString }));
    }

    [Test]
    public async Task Pages_and_counts()
    {
        var (_, middle, _) = await IngestThreeSentMinutesApart();

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(page: 2, pageSize: 1), new SortInfo(), true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { middle }));
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Filters_by_time_sent_range()
    {
        var (_, middle, newest) = await IngestThreeSentMinutesApart();

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), true,
            new DateTimeRange(BaseTimeSent, BaseTimeSent.AddMinutes(3)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { newest, middle }));
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Filters_by_endpoint()
    {
        var sales = new IngestedFailure();
        var billing = new IngestedFailure { ReceivingEndpoint = new EndpointDetails { Name = "Billing", Host = "BillingHost", HostId = Guid.NewGuid() } };

        await Ingest(sales, billing);

        var result = await MessagesViewStore.GetAllMessagesForEndpoint("Billing", new PagingInfo(), new SortInfo(), true);

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { billing.UniqueMessageIdString }));
    }

    [Test]
    public async Task Filters_by_conversation()
    {
        var conversationId = Guid.NewGuid().ToString();
        var inConversation = new IngestedFailure { ConversationId = conversationId };

        await Ingest(inConversation, new IngestedFailure());

        var result = await MessagesViewStore.GetAllMessagesByConversation(conversationId, new PagingInfo(), new SortInfo(), false);

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { inConversation.UniqueMessageIdString }));
    }

    /// <summary>
    /// The RavenDB persister ignores includeSystemMessages here, and so does this one: a
    /// conversation is incomplete without the system messages that took part in it.
    /// </summary>
    [Test]
    public async Task Keeps_system_messages_in_a_conversation()
    {
        var conversationId = Guid.NewGuid().ToString();
        var system = new IngestedFailure { ConversationId = conversationId, IsSystemMessage = true };

        await Ingest(system);

        var result = await MessagesViewStore.GetAllMessagesByConversation(conversationId, new PagingInfo(), new SortInfo(), false);

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { system.UniqueMessageIdString }));
    }

    [Test]
    public async Task Searches_the_headers()
    {
        var matching = new IngestedFailure { ExceptionMessage = "the zarquon overheated" };

        await Ingest(matching, new IngestedFailure());

        await AssertSearchFinds("zarquon", matching);
    }

    [Test]
    public async Task Searches_the_body()
    {
        var matching = new IngestedFailure { Body = Encoding.UTF8.GetBytes("<order>zarquon</order>") };

        await Ingest(matching, new IngestedFailure());

        await AssertSearchFinds("zarquon", matching);
    }

    /// <summary>
    /// The full name is a single token to the PostgreSQL parser, which is why the index carries a
    /// separator stripped copy of the message type.
    /// </summary>
    [Test]
    public async Task Searches_the_short_message_type()
    {
        var matching = new IngestedFailure { MessageType = "MyCompany.Sales.ZarquonOverheated" };

        await Ingest(matching, new IngestedFailure());

        await AssertSearchFinds("ZarquonOverheated", matching);
    }

    /// <summary>
    /// ServicePulse links to /messages/search/{messageId}, so the id has to be findable even though
    /// the two parsers tokenise it differently: the SQL Server word breaker splits it on the
    /// hyphens, PostgreSQL keeps it as a single uuid token.
    /// </summary>
    [Test]
    public async Task Searches_the_message_id()
    {
        var matching = new IngestedFailure();

        await Ingest(matching);

        await AssertSearchFinds(matching.MessageId, matching);
    }

    [Test]
    public async Task Ors_the_search_terms()
    {
        var first = new IngestedFailure { ExceptionMessage = "the zarquon overheated" };
        var second = new IngestedFailure { ExceptionMessage = "the flux capacitor melted" };

        await Ingest(first, second);

        await AssertSearchFinds("zarquon capacitor", first, second);
    }

    [Test]
    public async Task Searches_within_an_endpoint()
    {
        var billing = new IngestedFailure
        {
            ExceptionMessage = "the zarquon overheated",
            ReceivingEndpoint = new EndpointDetails { Name = "Billing", Host = "BillingHost", HostId = Guid.NewGuid() }
        };
        var sales = new IngestedFailure { ExceptionMessage = "the zarquon overheated" };

        await Ingest(billing, sales);

        await WaitForSearchResults(
            () => MessagesViewStore.SearchEndpointMessages("Billing", "zarquon", new PagingInfo(), new SortInfo()),
            billing);
    }

    static readonly DateTime BaseTimeSent = new(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);

    async Task<(string Oldest, string Middle, string Newest)> IngestThreeSentMinutesApart()
    {
        var oldest = new IngestedFailure { TimeSent = BaseTimeSent.AddMinutes(-2) };
        var middle = new IngestedFailure { TimeSent = BaseTimeSent };
        var newest = new IngestedFailure { TimeSent = BaseTimeSent.AddMinutes(2) };

        await Ingest(oldest, middle, newest);

        return (oldest.UniqueMessageIdString, middle.UniqueMessageIdString, newest.UniqueMessageIdString);
    }

    async Task<MessagesView> SingleMessage()
    {
        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), includeSystemMessages: true);

        return result.Results.Single();
    }

    Task AssertSearchFinds(string searchTerms, params IngestedFailure[] expected) =>
        WaitForSearchResults(
            () => MessagesViewStore.GetAllMessagesForSearch(searchTerms, new PagingInfo(), new SortInfo()),
            expected);

    /// <summary>
    /// SQL Server populates its full text index asynchronously, so a search right after the write
    /// legitimately returns nothing for a moment. PostgreSQL is current as soon as the transaction
    /// commits and passes on the first attempt.
    /// </summary>
    static async Task WaitForSearchResults(Func<Task<QueryResult<IList<MessagesView>>>> search, params IngestedFailure[] expected)
    {
        var expectedIds = expected.Select(failure => failure.UniqueMessageIdString).OrderBy(id => id).ToArray();
        IList<MessagesView> results = [];

        await WaitUntil(async () =>
        {
            results = (await search()).Results;

            return results.Count == expectedIds.Length;
        }, $"Search returned {expectedIds.Length} message(s)", TimeSpan.FromSeconds(30));

        Assert.That(results.Select(view => view.Id).OrderBy(id => id), Is.EqualTo(expectedIds));
    }

    async Task Insert(IngestedFailure failure, FailedMessageStatus status, int attempts)
    {
        var message = failure.ToFailedMessage(status, attempts);
        message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);

        await PersistenceTestsContext.InsertFailedMessages(message);
        await CompleteDatabaseOperation();
    }
}
