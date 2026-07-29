namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.EventLog;
using ServiceControl.Persistence.Infrastructure;

class EventLogDataStoreTests : PersistenceTestBase
{
    [Test]
    public async Task Added_item_is_returned_with_all_values_intact()
    {
        var raisedAt = new DateTime(2026, 7, 22, 10, 30, 0, DateTimeKind.Utc);
        var logItem = CreateLogItem("MessageFailed", raisedAt);
        logItem.Severity = Severity.Error;
        logItem.Description = "Message processing failed";
        logItem.RelatedTo = ["/message/abc123", "/endpoint/Sales"];

        await EventLogDataStore.Add(logItem, Guid.CreateVersion7());
        await CompleteDatabaseOperation();

        var (items, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        Assert.That(items, Has.Count.EqualTo(1));
        var stored = items[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.Description, Is.EqualTo("Message processing failed"));
            Assert.That(stored.Severity, Is.EqualTo(Severity.Error));
            Assert.That(stored.RaisedAt, Is.EqualTo(raisedAt));
            Assert.That(stored.Category, Is.EqualTo(logItem.Category));
            Assert.That(stored.EventType, Is.EqualTo("MessageFailed"));
            Assert.That(stored.RelatedTo, Is.EqualTo(["/message/abc123", "/endpoint/Sales"]));
        }
    }

    [Test]
    public async Task The_supplied_event_id_is_recoverable_from_the_stored_item()
    {
        var eventId = Guid.CreateVersion7();
        var logItem = CreateLogItem("MessageFailed", new DateTime(2026, 7, 22, 10, 30, 0, DateTimeKind.Utc));

        await EventLogDataStore.Add(logItem, eventId);
        await CompleteDatabaseOperation();

        var (items, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        // Not equality: Different persisters may return the event id in different formats.
        Assert.That(items[0].Id, Does.Contain(eventId.ToString()));
    }

    [Test]
    public async Task Item_with_no_related_links_round_trips_as_empty()
    {
        var logItem = CreateLogItem("EndpointStarted", DateTime.UtcNow);
        logItem.RelatedTo = [];

        await EventLogDataStore.Add(logItem, Guid.CreateVersion7());
        await CompleteDatabaseOperation();

        var (items, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        Assert.That(items[0].RelatedTo, Is.Empty);
    }

    [Test]
    public async Task Items_are_returned_most_recently_raised_first()
    {
        var baseTime = new DateTime(2026, 7, 22, 9, 0, 0, DateTimeKind.Utc);
        await EventLogDataStore.Add(CreateLogItem("Oldest", baseTime), Guid.CreateVersion7());
        await EventLogDataStore.Add(CreateLogItem("Newest", baseTime.AddMinutes(2)), Guid.CreateVersion7());
        await EventLogDataStore.Add(CreateLogItem("Middle", baseTime.AddMinutes(1)), Guid.CreateVersion7());
        await CompleteDatabaseOperation();

        var (items, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        Assert.That(items.Select(i => i.EventType), Is.EqualTo(new[] { "Newest", "Middle", "Oldest" }));
    }

    [Test]
    public async Task Empty_store_returns_no_items()
    {
        var (items, total, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(items, Is.Empty);
            Assert.That(total, Is.Zero);
        }
    }

    [Test]
    public async Task Page_size_limits_returned_items_but_not_the_total()
    {
        await AddItems(5);

        var (items, total, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 1, pageSize: 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(items, Has.Count.EqualTo(2));
            Assert.That(total, Is.EqualTo(5));
        }
    }

    [Test]
    public async Task Later_pages_continue_where_the_previous_page_ended()
    {
        await AddItems(5);

        var (firstPage, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 1, pageSize: 2));
        var (secondPage, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 2, pageSize: 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondPage, Has.Count.EqualTo(2));
            Assert.That(secondPage.Select(i => i.Id).Intersect(firstPage.Select(i => i.Id)), Is.Empty);
        }
    }

    [Test]
    public async Task Final_page_returns_only_the_remaining_items()
    {
        await AddItems(5);

        var (items, total, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 3, pageSize: 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(total, Is.EqualTo(5));
        }
    }

    [Test]
    public async Task Page_beyond_the_end_returns_no_items()
    {
        await AddItems(2);

        var (items, total, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 5, pageSize: 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(items, Is.Empty);
            Assert.That(total, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Version_changes_when_an_item_is_added()
    {
        await AddItems(1);
        var (_, _, versionBefore) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        await AddItems(1);
        var (_, _, versionAfter) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        Assert.That(versionAfter, Is.Not.EqualTo(versionBefore));
    }

    [Test]
    public async Task Version_is_stable_while_nothing_changes()
    {
        await AddItems(2);

        var (_, _, firstRead) = await EventLogDataStore.GetEventLogItems(new PagingInfo());
        var (_, _, secondRead) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        Assert.That(secondRead, Is.EqualTo(firstRead));
    }

    [Test]
    public async Task Matching_known_version_reports_no_items()
    {
        await AddItems(3);
        var (_, _, version) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        var (items, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(), version);

        Assert.That(items, Is.Null, "a caller already holding the current version must be told so, not handed the page again");
    }

    [Test]
    public async Task Matching_known_version_still_reports_total_and_version()
    {
        await AddItems(3);
        var (_, _, version) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        var (_, total, versionAgain) = await EventLogDataStore.GetEventLogItems(new PagingInfo(), version);

        using (Assert.EnterMultipleScope())
        {
            // The controller sets Total-Count and ETag on the 304, so neither may be dropped
            // just because the page was not fetched.
            Assert.That(total, Is.EqualTo(3));
            Assert.That(versionAgain, Is.EqualTo(version));
        }
    }

    [Test]
    public async Task Stale_known_version_returns_the_page()
    {
        await AddItems(2);
        var (_, _, staleVersion) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        await AddItems(1);

        var (items, total, freshVersion) = await EventLogDataStore.GetEventLogItems(new PagingInfo(), staleVersion);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(items, Is.Not.Null);
            Assert.That(items, Has.Count.EqualTo(3));
            Assert.That(total, Is.EqualTo(3));
            Assert.That(freshVersion, Is.Not.EqualTo(staleVersion));
        }
    }

    [Test]
    public async Task Unrecognised_known_version_returns_the_page()
    {
        await AddItems(2);

        var (items, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(), "not-a-version-this-store-ever-issued");

        Assert.That(items, Is.Not.Null, "an unrecognised validator must be treated as a cache miss, never as a match");
    }

    async Task AddItems(int count)
    {
        var baseTime = new DateTime(2026, 7, 22, 8, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < count; i++)
        {
            await EventLogDataStore.Add(CreateLogItem($"Event{i}", baseTime.AddMinutes(i)), Guid.CreateVersion7());
        }

        await CompleteDatabaseOperation();
    }

    static EventLogItem CreateLogItem(string eventType, DateTime raisedAt) => new()
    {
        Category = "Recoverability",
        EventType = eventType,
        Description = $"{eventType} occurred",
        Severity = Severity.Info,
        RaisedAt = raisedAt,
        RelatedTo = []
    };
}
