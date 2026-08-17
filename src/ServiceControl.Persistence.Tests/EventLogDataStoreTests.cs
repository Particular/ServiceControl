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

        await EventLogDataStore.Add(logItem);
        await CompleteDatabaseOperation();

        var items = (await EventLogDataStore.GetEventLogItems(new PagingInfo())).Results;

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
    public async Task Every_returned_item_carries_its_own_id()
    {
        var raisedAt = new DateTime(2026, 7, 22, 10, 30, 0, DateTimeKind.Utc);

        await EventLogDataStore.Add(CreateLogItem("MessageFailed", raisedAt));
        await EventLogDataStore.Add(CreateLogItem("EndpointStarted", raisedAt));
        await CompleteDatabaseOperation();

        var items = (await EventLogDataStore.GetEventLogItems(new PagingInfo())).Results;

        Assert.That(items, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(items[0].Id, Is.Not.Null.And.Not.Empty);
            Assert.That(items[1].Id, Is.Not.Null.And.Not.Empty);
            Assert.That(items[0].Id, Is.Not.EqualTo(items[1].Id));
        }
    }

    [Test]
    public async Task Item_with_no_related_links_round_trips_as_empty()
    {
        var logItem = CreateLogItem("EndpointStarted", DateTime.UtcNow);
        logItem.RelatedTo = [];

        await EventLogDataStore.Add(logItem);
        await CompleteDatabaseOperation();

        var items = (await EventLogDataStore.GetEventLogItems(new PagingInfo())).Results;

        Assert.That(items[0].RelatedTo, Is.Empty);
    }

    [Test]
    public async Task Items_are_returned_most_recently_raised_first()
    {
        var baseTime = new DateTime(2026, 7, 22, 9, 0, 0, DateTimeKind.Utc);
        await EventLogDataStore.Add(CreateLogItem("Oldest", baseTime));
        await EventLogDataStore.Add(CreateLogItem("Newest", baseTime.AddMinutes(2)));
        await EventLogDataStore.Add(CreateLogItem("Middle", baseTime.AddMinutes(1)));
        await CompleteDatabaseOperation();

        var items = (await EventLogDataStore.GetEventLogItems(new PagingInfo())).Results;

        Assert.That(items.Select(i => i.EventType), Is.EqualTo(["Newest", "Middle", "Oldest"]));
    }

    [Test]
    public async Task Empty_store_returns_no_items()
    {
        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results, Is.Empty);
            Assert.That(result.QueryStats.TotalCount, Is.Zero);
        }
    }

    [Test]
    public async Task Empty_store_is_a_page_of_nothing_rather_than_not_modified()
    {
        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NotModified, Is.False, "an empty store still has a representation to return");
            Assert.That(result.Results, Is.Not.Null);
        }
    }

    [Test]
    public async Task Page_size_limits_returned_items_but_not_the_total()
    {
        await AddItems(5);

        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 1, pageSize: 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results, Has.Count.EqualTo(2));
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(5));
        }
    }

    [Test]
    public async Task Later_pages_continue_where_the_previous_page_ended()
    {
        await AddItems(5);

        var firstPage = (await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 1, pageSize: 2))).Results;
        var secondPage = (await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 2, pageSize: 2))).Results;

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

        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 3, pageSize: 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results, Has.Count.EqualTo(1));
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(5));
        }
    }

    [Test]
    public async Task Page_beyond_the_end_returns_no_items()
    {
        await AddItems(2);

        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 5, pageSize: 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results, Is.Empty);
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Version_changes_when_an_item_is_added()
    {
        await AddItems(1);
        var versionBefore = await CurrentVersion();

        await AddItems(1);
        var versionAfter = await CurrentVersion();

        Assert.That(versionAfter, Is.Not.EqualTo(versionBefore));
    }

    [Test]
    public async Task Version_is_stable_while_nothing_changes()
    {
        await AddItems(2);

        var firstRead = await CurrentVersion();
        var secondRead = await CurrentVersion();

        Assert.That(secondRead, Is.EqualTo(firstRead));
    }

    [Test]
    public async Task Matching_known_version_reports_not_modified()
    {
        await AddItems(3);
        var version = await CurrentVersion();

        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo(), version);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NotModified, Is.True, "a caller already holding the current version must be told so, not handed the page again");
            Assert.That(result.Results, Is.Null, "a not-modified result carries no page");
        }
    }

    [Test]
    public async Task Matching_known_version_still_reports_total_and_version()
    {
        await AddItems(3);
        var version = await CurrentVersion();

        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo(), version);

        using (Assert.EnterMultipleScope())
        {
            // The controller sets Total-Count and ETag on the 304, so neither may be dropped
            // just because the page was not fetched.
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(3));
            Assert.That(result.QueryStats.ETag, Is.EqualTo(version));
        }
    }

    [Test]
    public async Task Stale_known_version_returns_the_page()
    {
        await AddItems(2);
        var staleVersion = await CurrentVersion();

        await AddItems(1);

        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo(), staleVersion);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NotModified, Is.False);
            Assert.That(result.Results, Has.Count.EqualTo(3));
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(3));
            Assert.That(result.QueryStats.ETag, Is.Not.EqualTo(staleVersion));
        }
    }

    [Test]
    public async Task Unrecognised_known_version_returns_the_page()
    {
        await AddItems(2);

        var result = await EventLogDataStore.GetEventLogItems(new PagingInfo(), "not-a-version-this-store-ever-issued");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NotModified, Is.False, "an unrecognised validator must be treated as a cache miss, never as a match");
            Assert.That(result.Results, Is.Not.Null);
        }
    }

    async Task<string> CurrentVersion() =>
        (await EventLogDataStore.GetEventLogItems(new PagingInfo())).QueryStats.ETag;

    async Task AddItems(int count)
    {
        var baseTime = new DateTime(2026, 7, 22, 8, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < count; i++)
        {
            await EventLogDataStore.Add(CreateLogItem($"Event{i}", baseTime.AddMinutes(i)));
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
