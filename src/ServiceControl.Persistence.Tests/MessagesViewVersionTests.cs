namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.CompositeViews.Messages;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
class MessagesViewVersionTests : IngestionTestBase
{
    [Test]
    public async Task Version_changes_when_a_message_is_added()
    {
        await Ingest(new IngestedFailure());
        await CompleteDatabaseOperation();

        var before = await AllMessages();

        await Ingest(new IngestedFailure());
        await CompleteDatabaseOperation();

        var after = await AllMessages();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.Results, Has.Count.EqualTo(2), "the body now reports two messages");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the body changed, so the validator must too");
        }
    }

    [Test]
    public async Task Version_changes_when_a_message_is_re_ingested_and_the_count_does_not_move()
    {
        var failure = new IngestedFailure();

        await Ingest(failure);
        await CompleteDatabaseOperation();

        var before = await AllMessages();

        AdvanceClock(TimeSpan.FromMinutes(5));

        await Ingest(failure.NextAttempt(failure.AttemptedAt.AddMinutes(5)));
        await CompleteDatabaseOperation();

        var after = await AllMessages();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.Results, Has.Count.EqualTo(1), "still one message");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the message the body reports has a later attempt on it, so the validator cannot stay put");
        }
    }

    [Test]
    public async Task Version_changes_when_the_endpoint_being_queried_gains_a_message()
    {
        await Ingest(ReceivedBy("Sales"));
        await Ingest(ReceivedBy("Shipping"));
        await CompleteDatabaseOperation();

        var before = await MessagesFor("Sales");

        await Ingest(ReceivedBy("Sales"));
        await CompleteDatabaseOperation();

        var after = await MessagesFor("Sales");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before.Results, Has.Count.EqualTo(1), "the Shipping message is not on this page");
            Assert.That(after.Results, Has.Count.EqualTo(2), "and the body now reports two");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the body changed, so the validator must too");
        }
    }

    [Test]
    public async Task Version_is_stable_while_nothing_changes()
    {
        await Ingest(new IngestedFailure());
        await CompleteDatabaseOperation();

        var first = await AllMessages();
        var second = await AllMessages();

        VersionAssert.Matches(first.QueryStats.Version, second.QueryStats.Version,
            "nothing changed, so the validator has to stay put or conditional GET never pays off");
    }

    [Test]
    public async Task Version_changes_when_the_total_moves_under_an_unchanged_page()
    {
        var shown = new IngestedFailure();

        await Ingest(shown);
        await CompleteDatabaseOperation();

        var before = await MessagesViewStore.GetAllMessages(new PagingInfo(page: 1, pageSize: 1), new SortInfo(), includeSystemMessages: true);

        AdvanceClock(TimeSpan.FromMinutes(5));

        await Ingest(new IngestedFailure());
        await CompleteDatabaseOperation();

        var after = await MessagesViewStore.GetAllMessages(new PagingInfo(page: 1, pageSize: 1), new SortInfo(), includeSystemMessages: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.Results, Has.Count.EqualTo(1), "still one row on the page");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "Total-Count went from one to two, and the body reports it, so the validator has to move");
        }
    }

    [Test]
    public async Task Version_changes_when_a_row_on_the_page_changes_status()
    {
        var archived = new IngestedFailure();

        await Ingest(archived, new IngestedFailure());
        await CompleteDatabaseOperation();

        var before = await AllMessages();

        await FailedMessageLifecycleStore.MarkAsArchived(archived.UniqueMessageIdString);
        await CompleteDatabaseOperation();

        var after = await AllMessages();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.Results, Has.Count.EqualTo(2), "both messages are still on the page");
            Assert.That(after.QueryStats.TotalCount, Is.EqualTo(before.QueryStats.TotalCount), "and the total has not moved");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the body reports the new status, so a revalidating client must not be told its page is current");
        }
    }

    [Test]
    public async Task An_empty_store_still_reports_a_version()
    {
        var result = await AllMessages();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results, Is.Empty);
            Assert.That(result.QueryStats.Version.HasValue, Is.True,
                "an empty list is a representation like any other and has to be cacheable");
        }
    }

    static IngestedFailure ReceivedBy(string endpointName) =>
        new()
        {
            EndpointName = endpointName,
            ReceivingEndpoint = new EndpointDetails { Name = endpointName, Host = "ReceiverHost", HostId = Guid.NewGuid() }
        };

    Task<QueryResult<IList<MessagesView>>> AllMessages() =>
        MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), includeSystemMessages: true);

    Task<QueryResult<IList<MessagesView>>> MessagesFor(string endpointName) =>
        MessagesViewStore.GetAllMessagesForEndpoint(endpointName, new PagingInfo(), new SortInfo(), includeSystemMessages: true);
}
