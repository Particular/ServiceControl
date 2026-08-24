namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;

class FailedMessageQueryDataStoreTests : PersistenceTestBase
{
    [Test]
    public async Task Returns_a_stored_failure()
    {
        var failure = new IngestedFailure
        {
            ExceptionSource = "MyCompany.Sales.Handlers",
            ExceptionStackTrace = "   at MyCompany.Sales.Handlers.OrderPlacedHandler.Handle()"
        };

        await Insert(failure);

        var result = await FailedMessageQueryStore.GetFailedMessages(null, null, null, new PagingInfo(), new SortInfo());

        var view = result.Results.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Id, Is.EqualTo(failure.UniqueMessageIdString));
            Assert.That(view.MessageId, Is.EqualTo(failure.MessageId));
            Assert.That(view.MessageType, Is.EqualTo(failure.MessageType));
            Assert.That(view.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(view.TimeSent, Is.EqualTo(failure.TimeSent));
            Assert.That(view.TimeOfFailure, Is.EqualTo(failure.TimeOfFailure));
            Assert.That(view.NumberOfProcessingAttempts, Is.EqualTo(1));
            Assert.That(view.QueueAddress, Is.EqualTo(failure.QueueAddress));
            Assert.That(view.SendingEndpoint.Name, Is.EqualTo(failure.SendingEndpoint.Name));
            Assert.That(view.ReceivingEndpoint.Name, Is.EqualTo(failure.ReceivingEndpoint.Name));
            Assert.That(view.Exception.ExceptionType, Is.EqualTo(failure.ExceptionType));
            Assert.That(view.Exception.Message, Is.EqualTo(failure.ExceptionMessage));
        }
    }

    [Test]
    public async Task Exposes_the_full_exception_details()
    {
        var failure = new IngestedFailure
        {
            ExceptionSource = "MyCompany.Sales.Handlers",
            ExceptionStackTrace = "   at MyCompany.Sales.Handlers.OrderPlacedHandler.Handle()"
        };

        await Insert(failure);

        var view = await FailedMessageQueryStore.GetLatestFailedMessageView(failure.UniqueMessageIdString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view.Exception.Source, Is.EqualTo(failure.ExceptionSource));
            Assert.That(view.Exception.StackTrace, Is.EqualTo(failure.ExceptionStackTrace));
        }
    }

    [Test]
    public async Task Reports_an_edited_message()
    {
        var original = new IngestedFailure();
        var edited = new IngestedFailure { EditOf = original.UniqueMessageIdString };

        await Insert(original, edited);

        var originalView = await FailedMessageQueryStore.GetLatestFailedMessageView(original.UniqueMessageIdString);
        var editedView = await FailedMessageQueryStore.GetLatestFailedMessageView(edited.UniqueMessageIdString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(originalView.Edited, Is.False);
            Assert.That(originalView.EditOf, Is.Empty);
            Assert.That(editedView.Edited, Is.True);
            Assert.That(editedView.EditOf, Is.EqualTo(original.UniqueMessageIdString));
        }
    }

    [Test]
    public async Task Filters_by_a_single_status()
    {
        var unresolved = new IngestedFailure();
        var archived = new IngestedFailure();

        await Insert(unresolved.ToFailedMessage(), archived.ToFailedMessage(FailedMessageStatus.Archived));

        var result = await FailedMessageQueryStore.GetFailedMessages("archived", null, null, new PagingInfo(), new SortInfo());

        Assert.That(Ids(result), Is.EqualTo(new[] { archived.UniqueMessageIdString }));
    }

    [Test]
    public async Task Filters_by_several_statuses()
    {
        var unresolved = new IngestedFailure();
        var archived = new IngestedFailure();
        var resolved = new IngestedFailure();

        await Insert(
            unresolved.ToFailedMessage(),
            archived.ToFailedMessage(FailedMessageStatus.Archived),
            resolved.ToFailedMessage(FailedMessageStatus.Resolved));

        var result = await FailedMessageQueryStore.GetFailedMessages("unresolved,archived", null, null, new PagingInfo(), new SortInfo());

        Assert.That(Ids(result), Is.EquivalentTo(new[] { unresolved.UniqueMessageIdString, archived.UniqueMessageIdString }));
    }

    // A leading '-' excludes rather than includes, which RavenDB has supported for as long as the
    // status parameter has existed. No caller in this repository uses the form, but it is part of
    // the API contract, so the persisters have to agree on it.
    [Test]
    public async Task Excludes_a_status_prefixed_with_a_minus()
    {
        var unresolved = new IngestedFailure();
        var archived = new IngestedFailure();

        await Insert(unresolved.ToFailedMessage(), archived.ToFailedMessage(FailedMessageStatus.Archived));

        var result = await FailedMessageQueryStore.GetFailedMessages("-archived", null, null, new PagingInfo(), new SortInfo());

        Assert.That(Ids(result), Is.EqualTo(new[] { unresolved.UniqueMessageIdString }));
    }

    [Test]
    public async Task Ignores_an_unknown_status()
    {
        var failure = new IngestedFailure();

        await Insert(failure);

        var result = await FailedMessageQueryStore.GetFailedMessages("not_a_status", null, null, new PagingInfo(), new SortInfo());

        Assert.That(Ids(result), Is.EqualTo(new[] { failure.UniqueMessageIdString }));
    }

    [Test]
    public async Task Filters_by_a_modified_range()
    {
        var failure = new IngestedFailure();

        await Insert(failure);

        var enclosing = Range(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
        var past = Range(DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));

        var included = await FailedMessageQueryStore.GetFailedMessages(null, enclosing, null, new PagingInfo(), new SortInfo());
        var excluded = await FailedMessageQueryStore.GetFailedMessages(null, past, null, new PagingInfo(), new SortInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Ids(included), Is.EqualTo(new[] { failure.UniqueMessageIdString }));
            Assert.That(Ids(excluded), Is.Empty);
        }
    }

    [Test]
    public void Rejects_a_malformed_modified_range()
    {
        Assert.That(
            async () => await FailedMessageQueryStore.GetFailedMessages(null, "2016-03-11T00:27:15.474Z", null, new PagingInfo(), new SortInfo()),
            Throws.Exception.With.Message.Contains("Invalid modified date range"));
    }

    [Test]
    public async Task Filters_by_the_failing_endpoint_address()
    {
        var matching = new IngestedFailure { QueueAddress = "error", FailingEndpointAddress = "Sales@MACHINE" };
        var other = new IngestedFailure { QueueAddress = "error", FailingEndpointAddress = "Billing@MACHINE" };

        await Insert(matching, other);

        var result = await FailedMessageQueryStore.GetFailedMessages(null, null, "Sales@MACHINE", new PagingInfo(), new SortInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Ids(result), Is.EqualTo(new[] { matching.UniqueMessageIdString }));
            Assert.That(result.Results.Single().QueueAddress, Is.EqualTo("Sales@MACHINE"));
        }
    }

    [Test]
    public async Task Matches_the_failing_endpoint_address_regardless_of_case()
    {
        var failure = new IngestedFailure { FailingEndpointAddress = "Sales@MACHINE" };

        await Insert(failure);

        var result = await FailedMessageQueryStore.GetFailedMessages(null, null, "sales@machine", new PagingInfo(), new SortInfo());

        Assert.That(Ids(result), Is.EqualTo(new[] { failure.UniqueMessageIdString }));
    }

    [Test]
    public async Task Filters_by_endpoint()
    {
        var sales = new IngestedFailure();
        var billing = new IngestedFailure
        {
            ReceivingEndpoint = new EndpointDetails { Name = "Billing", Host = "Host", HostId = Guid.NewGuid() }
        };

        await Insert(sales, billing);

        var result = await FailedMessageQueryStore.GetFailedMessagesByEndpoint(null, "Billing", null, new PagingInfo(), new SortInfo());

        Assert.That(Ids(result), Is.EqualTo(new[] { billing.UniqueMessageIdString }));
    }

    [TestCase("time_sent", "asc")]
    [TestCase("time_sent", "desc")]
    [TestCase("message_type", "asc")]
    [TestCase("message_type", "desc")]
    [TestCase("status", "asc")]
    [TestCase("status", "desc")]
    [TestCase("time_of_failure", "asc")]
    [TestCase("time_of_failure", "desc")]
    public async Task Sorts_by_the_requested_field(string sort, string direction)
    {
        var baseTime = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

        var first = new IngestedFailure
        {
            MessageType = "AAA.First",
            TimeSent = baseTime,
            AttemptedAt = baseTime.AddMinutes(1),
            TimeOfFailure = baseTime.AddMinutes(1)
        };

        var second = new IngestedFailure
        {
            MessageType = "ZZZ.Second",
            TimeSent = baseTime.AddHours(1),
            AttemptedAt = baseTime.AddHours(1).AddMinutes(1),
            TimeOfFailure = baseTime.AddHours(1).AddMinutes(1)
        };

        await Insert(
            first.ToFailedMessage(),
            second.ToFailedMessage(FailedMessageStatus.Archived));

        var result = await FailedMessageQueryStore.GetFailedMessages(null, null, null, new PagingInfo(), new SortInfo(sort, direction));

        var expected = direction == "asc"
            ? new[] { first.UniqueMessageIdString, second.UniqueMessageIdString }
            : new[] { second.UniqueMessageIdString, first.UniqueMessageIdString };

        Assert.That(Ids(result), Is.EqualTo(expected));
    }

    [Test]
    public async Task Falls_back_to_time_sent_for_an_unknown_sort()
    {
        var baseTime = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);
        var older = new IngestedFailure { TimeSent = baseTime };
        var newer = new IngestedFailure { TimeSent = baseTime.AddHours(1) };

        await Insert(older, newer);

        var result = await FailedMessageQueryStore.GetFailedMessages(null, null, null, new PagingInfo(), new SortInfo("not_a_field", "desc"));

        Assert.That(Ids(result), Is.EqualTo(new[] { newer.UniqueMessageIdString, older.UniqueMessageIdString }));
    }

    [Test]
    public async Task Pages_through_the_results()
    {
        var baseTime = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc);

        var failures = Enumerable.Range(0, 5)
            .Select(i => new IngestedFailure { TimeSent = baseTime.AddMinutes(i) })
            .ToArray();

        await Insert(failures);

        var firstPage = await FailedMessageQueryStore.GetFailedMessages(null, null, null, new PagingInfo(1, 2), new SortInfo("time_sent", "asc"));
        var secondPage = await FailedMessageQueryStore.GetFailedMessages(null, null, null, new PagingInfo(2, 2), new SortInfo("time_sent", "asc"));
        var lastPage = await FailedMessageQueryStore.GetFailedMessages(null, null, null, new PagingInfo(3, 2), new SortInfo("time_sent", "asc"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Ids(firstPage), Is.EqualTo(failures.Take(2).Select(f => f.UniqueMessageIdString)));
            Assert.That(Ids(secondPage), Is.EqualTo(failures.Skip(2).Take(2).Select(f => f.UniqueMessageIdString)));
            Assert.That(Ids(lastPage), Is.EqualTo(failures.Skip(4).Select(f => f.UniqueMessageIdString)));

            // The total is the size of the whole filtered set, not of the page.
            Assert.That(firstPage.QueryStats.TotalCount, Is.EqualTo(5));
        }
    }

    [Test]
    public async Task Reports_stats_matching_the_query()
    {
        var unresolved = new IngestedFailure();
        var archived = new IngestedFailure();

        await Insert(unresolved.ToFailedMessage(), archived.ToFailedMessage(FailedMessageStatus.Archived));

        var query = await FailedMessageQueryStore.GetFailedMessages("unresolved", null, null, new PagingInfo(), new SortInfo());
        var stats = await FailedMessageQueryStore.GetFailedMessagesStats("unresolved", null, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stats.TotalCount, Is.EqualTo(1));
            Assert.That(stats.TotalCount, Is.EqualTo(query.QueryStats.TotalCount));
            Assert.That(stats.Version.HasValue, Is.True, "the count endpoint still has to be cacheable");
        }
    }

    [Test]
    public async Task Repeats_the_etag_while_nothing_changes()
    {
        await Insert(new IngestedFailure());

        var first = await FailedMessageQueryStore.GetFailedMessagesStats(null, null, null);
        var second = await FailedMessageQueryStore.GetFailedMessagesStats(null, null, null);

        Assert.That(second.Version.Matches(first.Version), Is.True);
    }

    [Test]
    public async Task Changes_the_etag_when_the_set_changes()
    {
        await Insert(new IngestedFailure());

        var before = await FailedMessageQueryStore.GetFailedMessagesStats(null, null, null);

        await Insert(new IngestedFailure());

        var after = await FailedMessageQueryStore.GetFailedMessagesStats(null, null, null);

        Assert.That(after.Version.Matches(before.Version), Is.False);
    }


    [Test]
    public async Task Returns_the_stored_message_by_id()
    {
        var failure = new IngestedFailure();

        await Insert(failure);

        var message = await FailedMessageQueryStore.GetFailedMessage(failure.UniqueMessageIdString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.UniqueMessageId, Is.EqualTo(failure.UniqueMessageIdString));
            Assert.That(message.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(message.ProcessingAttempts.Last().MessageId, Is.EqualTo(failure.MessageId));
            Assert.That(message.ProcessingAttempts.Last().FailureDetails.AddressOfFailingEndpoint, Is.EqualTo(failure.QueueAddress));
            Assert.That(message.FailureGroups.Select(g => g.Id), Is.EquivalentTo(failure.Groups.Select(g => g.Id)));
        }
    }

    [Test]
    public async Task Reports_the_number_of_attempts_on_the_stored_message()
    {
        var failure = new IngestedFailure();

        await Insert(failure.ToFailedMessage(numberOfAttempts: 3));

        var message = await FailedMessageQueryStore.GetFailedMessage(failure.UniqueMessageIdString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.ProcessingAttempts, Has.Count.EqualTo(3));
            Assert.That(message.ProcessingAttempts.Last().MessageId, Is.EqualTo(failure.MessageId));
        }
    }

    [Test]
    public async Task Returns_nothing_for_an_unknown_id()
    {
        var view = await FailedMessageQueryStore.GetLatestFailedMessageView(Guid.NewGuid().ToString());
        var message = await FailedMessageQueryStore.GetFailedMessage(Guid.NewGuid().ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(view, Is.Null);
            Assert.That(message, Is.Null);
        }
    }

    [Test]
    public async Task Returns_the_stored_messages_by_ids()
    {
        var wanted = new IngestedFailure();
        var alsoWanted = new IngestedFailure();
        var other = new IngestedFailure();

        await Insert(wanted, alsoWanted, other);

        var messages = await FailedMessageQueryStore.GetFailedMessagesByIds([wanted.UniqueMessageId, alsoWanted.UniqueMessageId]);

        Assert.That(
            messages.Select(m => m.UniqueMessageId),
            Is.EquivalentTo(new[] { wanted.UniqueMessageIdString, alsoWanted.UniqueMessageIdString }));
    }

    [Test]
    public async Task Skips_ids_that_are_not_stored()
    {
        var stored = new IngestedFailure();

        await Insert(stored);

        var messages = await FailedMessageQueryStore.GetFailedMessagesByIds([stored.UniqueMessageId, Guid.NewGuid()]);

        Assert.That(messages.Select(m => m.UniqueMessageId), Is.EqualTo(new[] { stored.UniqueMessageIdString }));
    }

    Task Insert(params IngestedFailure[] failures) =>
        Insert([.. failures.Select(failure => failure.ToFailedMessage())]);

    async Task Insert(params FailedMessage[] messages)
    {
        foreach (var message in messages)
        {
            message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);
        }

        await PersistenceTestsContext.InsertFailedMessages(messages);
        await CompleteDatabaseOperation();
    }

    static IEnumerable<string> Ids(QueryResult<IList<FailedMessageView>> result) =>
        result.Results.Select(view => view.Id);

    static string Range(DateTime from, DateTime to) => $"{from:O}...{to:O}";
}
