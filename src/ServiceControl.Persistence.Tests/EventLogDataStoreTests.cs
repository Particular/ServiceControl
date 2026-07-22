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

        var (items, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        Assert.That(items, Has.Count.EqualTo(1));
        var stored = items[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.Id, Is.EqualTo(logItem.Id));
            Assert.That(stored.Description, Is.EqualTo("Message processing failed"));
            Assert.That(stored.Severity, Is.EqualTo(Severity.Error));
            Assert.That(stored.RaisedAt, Is.EqualTo(raisedAt));
            Assert.That(stored.Category, Is.EqualTo(logItem.Category));
            Assert.That(stored.EventType, Is.EqualTo("MessageFailed"));
            Assert.That(stored.RelatedTo, Is.EqualTo(["/message/abc123", "/endpoint/Sales"]));
        }
    }

    [Test]
    public async Task Item_with_no_related_links_round_trips_as_empty()
    {
        var logItem = CreateLogItem("EndpointStarted", DateTime.UtcNow);
        logItem.RelatedTo = [];

        await EventLogDataStore.Add(logItem);
        await CompleteDatabaseOperation();

        var (items, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

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
        Id = $"EventLogItem/Recoverability/{eventType}/{Guid.NewGuid()}",
        Category = "Recoverability",
        EventType = eventType,
        Description = $"{eventType} occurred",
        Severity = Severity.Info,
        RaisedAt = raisedAt,
        RelatedTo = []
    };
}
