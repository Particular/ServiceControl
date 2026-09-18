namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;

class AuditMessagesViewTests : AuditIngestionTestBase
{
    [Test]
    public async Task Reports_an_audited_message()
    {
        var audit = new IngestedAudit();

        await IngestAudit(audit);

        var view = (await All()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.EqualTo(audit.UniqueMessageIdString));
            Assert.That(view.MessageId, Is.EqualTo(audit.MessageId));
            Assert.That(view.MessageType, Is.EqualTo(audit.MessageType));
            Assert.That(view.Status, Is.EqualTo(MessageStatus.Successful));
            Assert.That(view.TimeSent, Is.EqualTo(audit.TimeSent));
            Assert.That(view.ProcessedAt, Is.EqualTo(audit.ProcessingEnded));
            Assert.That(view.CriticalTime, Is.EqualTo(audit.ProcessingEnded - audit.TimeSent));
            Assert.That(view.ProcessingTime, Is.EqualTo(audit.ProcessingEnded - audit.ProcessingStarted));
            Assert.That(view.DeliveryTime, Is.EqualTo(audit.ProcessingStarted - audit.TimeSent));
            Assert.That(view.ReceivingEndpoint.Name, Is.EqualTo(audit.ReceivingEndpoint.Name));
            Assert.That(view.ReceivingEndpoint.HostId, Is.EqualTo(audit.ReceivingEndpoint.HostId));
            Assert.That(view.SendingEndpoint.Name, Is.EqualTo(audit.SendingEndpoint.Name));
            Assert.That(view.ConversationId, Is.EqualTo(audit.ConversationId));
            Assert.That(view.MessageIntent, Is.EqualTo(audit.MessageIntent));
            Assert.That(view.BodyUrl, Is.EqualTo($"/messages/{audit.UniqueMessageIdString}/body"));
            Assert.That(view.BodySize, Is.EqualTo(audit.Body.Length));
            Assert.That(view.Headers.Select(header => header.Key), Does.Contain(NServiceBus.Headers.MessageId));
        }
    }

    [Test]
    public async Task Reports_the_saga_relationships_from_the_headers()
    {
        var sagaId = Guid.NewGuid();
        var audit = new IngestedAudit();
        audit.Headers["NServiceBus.InvokedSagas"] = $"MyCompany.Sales.OrderSaga:{sagaId}";
        audit.Headers["ServiceControl.SagaStateChange"] = $"{sagaId}:Updated";

        await IngestAudit(audit);

        var view = (await All()).Single();

        Assert.That(view.InvokedSagas, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.InvokedSagas[0].SagaId, Is.EqualTo(sagaId));
            Assert.That(view.InvokedSagas[0].SagaType, Is.EqualTo("MyCompany.Sales.OrderSaga"));
            Assert.That(view.InvokedSagas[0].ChangeStatus, Is.EqualTo("Updated"));
        }
    }

    [TestCase(FailedMessageStatus.Unresolved, MessageStatus.Failed)]
    [TestCase(FailedMessageStatus.Resolved, MessageStatus.ResolvedSuccessfully)]
    [TestCase(FailedMessageStatus.Archived, MessageStatus.ArchivedFailure)]
    public async Task A_failed_message_wins_over_its_audit_row_whatever_its_status(FailedMessageStatus failedStatus, MessageStatus reported)
    {
        var failure = new IngestedFailure();
        await SeedFailedMessage(failure.ToFailedMessage(failedStatus));
        await IngestAudit(new IngestedAudit { RetryOf = failure.UniqueMessageIdString });

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), includeSystemMessages: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { failure.UniqueMessageIdString }));
            Assert.That(result.Results.Single().Status, Is.EqualTo(reported));
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(1), "a message in both tables counts once");
        }
    }

    [Test]
    public async Task Pages_exactly_across_both_sources()
    {
        var sent = new DateTime(2026, 7, 22, 9, 0, 0, DateTimeKind.Utc);
        var failures = Enumerable.Range(0, 3).Select(i => new IngestedFailure { TimeSent = sent.AddMinutes(i * 2) }).ToArray();
        var audits = Enumerable.Range(0, 3).Select(i => new IngestedAudit { TimeSent = sent.AddMinutes((i * 2) + 1) }).ToArray();

        await Ingest(failures);
        await IngestAudit(audits);

        var expected = failures.Select(f => (Id: f.UniqueMessageIdString, TimeSent: f.TimeSent.Value))
            .Concat(audits.Select(a => (Id: a.UniqueMessageIdString, a.TimeSent)))
            .OrderByDescending(m => m.TimeSent)
            .Select(m => m.Id)
            .ToArray();

        var pages = new List<string>();
        long total = 0;

        for (var page = 1; page <= 3; page++)
        {
            var result = await MessagesViewStore.GetAllMessages(new PagingInfo(page, 2), new SortInfo("time_sent", "desc"), includeSystemMessages: true);
            pages.AddRange(result.Results.Select(view => view.Id));
            total = result.QueryStats.TotalCount;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pages, Is.EqualTo(expected), "the pages are contiguous and interleave both sources by time sent");
            Assert.That(total, Is.EqualTo(6));
        }
    }

    [Test]
    public async Task Counts_each_message_once_across_sources()
    {
        var failures = new[] { new IngestedFailure(), new IngestedFailure() };
        await Ingest(failures);
        await IngestAudit(new IngestedAudit { RetryOf = failures[0].UniqueMessageIdString }, new IngestedAudit());

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(1, 1), new SortInfo(), includeSystemMessages: true);

        Assert.That(result.QueryStats.TotalCount, Is.EqualTo(3));
    }

    [Test]
    public async Task Sorts_by_processed_at_across_sources()
    {
        var at = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
        var failure = new IngestedFailure { AttemptedAt = at.AddMinutes(1) };
        var earlier = new IngestedAudit { ProcessingEnded = at };
        var later = new IngestedAudit { ProcessingEnded = at.AddMinutes(2) };

        await Ingest(failure);
        await IngestAudit(earlier, later);

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo("processed_at", "asc"), includeSystemMessages: true);

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { earlier.UniqueMessageIdString, failure.UniqueMessageIdString, later.UniqueMessageIdString }));
    }

    [Test]
    public async Task Sorts_by_critical_time_with_failed_messages_at_zero()
    {
        var failure = new IngestedFailure();
        var audit = new IngestedAudit();

        await Ingest(failure);
        await IngestAudit(audit);

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo("critical_time", "desc"), includeSystemMessages: true);

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { audit.UniqueMessageIdString, failure.UniqueMessageIdString }));
    }

    [Test]
    public async Task Hides_audited_system_messages_unless_asked()
    {
        var system = new IngestedAudit { IsSystemMessage = true };
        var ordinary = new IngestedAudit();

        await IngestAudit(system, ordinary);

        var hidden = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), includeSystemMessages: false);
        var shown = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), includeSystemMessages: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hidden.Results.Select(view => view.Id), Is.EqualTo(new[] { ordinary.UniqueMessageIdString }));
            Assert.That(hidden.QueryStats.TotalCount, Is.EqualTo(1));
            Assert.That(shown.Results, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public async Task Filters_audited_messages_by_time_sent_range()
    {
        var sent = new DateTime(2026, 7, 22, 9, 0, 0, DateTimeKind.Utc);
        var inside = new IngestedAudit { TimeSent = sent };
        var outside = new IngestedAudit { TimeSent = sent.AddHours(2) };

        await IngestAudit(inside, outside);

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), includeSystemMessages: true, new DateTimeRange(sent.AddMinutes(-1), sent.AddMinutes(1)));

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { inside.UniqueMessageIdString }));
    }

    [Test]
    public async Task Filters_by_endpoint_across_sources()
    {
        var failure = new IngestedFailure { EndpointName = "Sales", ReceivingEndpoint = new() { Name = "Sales", Host = "h", HostId = Guid.NewGuid() } };
        var audit = new IngestedAudit { EndpointName = "Sales", ReceivingEndpoint = new() { Name = "Sales", Host = "h", HostId = Guid.NewGuid() } };
        var other = new IngestedAudit { EndpointName = "Billing", ReceivingEndpoint = new() { Name = "Billing", Host = "h", HostId = Guid.NewGuid() } };

        await Ingest(failure);
        await IngestAudit(audit, other);

        var result = await MessagesViewStore.GetAllMessagesForEndpoint("Sales", new PagingInfo(), new SortInfo(), includeSystemMessages: true);

        Assert.That(result.Results.Select(view => view.Id).OrderBy(id => id), Is.EqualTo(new[] { failure.UniqueMessageIdString, audit.UniqueMessageIdString }.OrderBy(id => id)));
    }

    [Test]
    public async Task A_conversation_spans_both_sources()
    {
        var conversationId = Guid.NewGuid().ToString();
        var failure = new IngestedFailure { ConversationId = conversationId };
        var audit = new IngestedAudit { ConversationId = conversationId, IsSystemMessage = true };
        var unrelated = new IngestedAudit();

        await Ingest(failure);
        await IngestAudit(audit, unrelated);

        var result = await MessagesViewStore.GetAllMessagesByConversation(conversationId, new PagingInfo(), new SortInfo(), includeSystemMessages: false);

        Assert.That(result.Results.Select(view => view.Id).OrderBy(id => id), Is.EqualTo(new[] { failure.UniqueMessageIdString, audit.UniqueMessageIdString }.OrderBy(id => id)));
    }

    [Test]
    public async Task Searches_audited_headers()
    {
        var matching = new IngestedAudit { ConversationId = "zarquon-conversation" };

        await IngestAudit(matching, new IngestedAudit());

        await AssertSearchFinds("zarquon", matching.UniqueMessageIdString);
    }

    [Test]
    public async Task Searches_audited_bodies()
    {
        var matching = new IngestedAudit { Body = Encoding.UTF8.GetBytes("<order>slartibartfast</order>") };

        await IngestAudit(matching, new IngestedAudit());

        await AssertSearchFinds("slartibartfast", matching.UniqueMessageIdString);
    }

    [Test]
    public async Task Searches_the_short_audited_message_type()
    {
        var matching = new IngestedAudit { MessageType = "MyCompany.Sales.Hooloovoo" };

        await IngestAudit(matching, new IngestedAudit());

        await AssertSearchFinds("Hooloovoo", matching.UniqueMessageIdString);
    }

    [Test]
    public async Task Searches_across_both_sources()
    {
        var failure = new IngestedFailure { ExceptionMessage = "the vogon overheated" };
        var audit = new IngestedAudit { ConversationId = "vogon-conversation" };

        await Ingest(failure);
        await IngestAudit(audit, new IngestedAudit());

        await AssertSearchFinds("vogon", failure.UniqueMessageIdString, audit.UniqueMessageIdString);
    }

    [Test]
    public async Task Searches_within_an_endpoint_across_sources()
    {
        var matching = new IngestedAudit { EndpointName = "Sales", ReceivingEndpoint = new() { Name = "Sales", Host = "h", HostId = Guid.NewGuid() }, ConversationId = "magrathea-conversation" };
        var elsewhere = new IngestedAudit { EndpointName = "Billing", ReceivingEndpoint = new() { Name = "Billing", Host = "h", HostId = Guid.NewGuid() }, ConversationId = "magrathea-conversation" };

        await IngestAudit(matching, elsewhere);

        await WaitForSearchResults(
            () => MessagesViewStore.SearchEndpointMessages("Sales", "magrathea", new PagingInfo(), new SortInfo()),
            matching.UniqueMessageIdString);
    }

    async Task<IList<MessagesView>> All() =>
        (await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), includeSystemMessages: true)).Results;

    Task AssertSearchFinds(string searchTerms, params string[] expectedIds) =>
        WaitForSearchResults(() => MessagesViewStore.GetAllMessagesForSearch(searchTerms, new PagingInfo(), new SortInfo()), expectedIds);

    // SQL Server populates its full text index asynchronously, so a search right after the write
    // legitimately returns nothing for a moment.
    static async Task WaitForSearchResults(Func<Task<QueryResult<IList<MessagesView>>>> search, params string[] expectedIds)
    {
        IList<MessagesView> results = [];

        await WaitUntil(async () =>
        {
            results = (await search()).Results;

            return results.Count == expectedIds.Length;
        }, $"Search returned {expectedIds.Length} message(s)", TimeSpan.FromSeconds(30));

        Assert.That(results.Select(view => view.Id).OrderBy(id => id), Is.EqualTo(expectedIds.OrderBy(id => id)));
    }
}
