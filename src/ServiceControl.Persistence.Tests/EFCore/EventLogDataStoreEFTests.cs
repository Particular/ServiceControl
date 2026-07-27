namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.EventLog;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.Infrastructure;

// Behaviour only the EF Core persisters guarantee, so it cannot live in the shared root folder.
// RavenDB orders event log items by RaisedAt alone.
class EventLogDataStoreEFTests : PersistenceTestBase
{
    // All ten items share one RaisedAt. Without the key as a tiebreaker in both the index and the
    // ORDER BY, the engine is free to return them in a different order per query, and items would
    // silently appear on two pages, or on none.
    [Test]
    public async Task Items_sharing_a_raised_at_page_without_overlap_or_omission()
    {
        var sameInstant = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
        var expected = new List<string>();

        for (var i = 0; i < 10; i++)
        {
            var item = CreateLogItem($"Collision{i}", sameInstant);
            expected.Add(item.Id);
            await EventLogDataStore.Add(item);
        }

        await CompleteDatabaseOperation();

        var paged = new List<string>();
        for (var page = 1; page <= 5; page++)
        {
            var (items, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: page, pageSize: 2));
            paged.AddRange(items.Select(i => i.Id));
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paged, Has.Count.EqualTo(10), "every item must appear exactly once across the pages");
            Assert.That(paged.Distinct().Count(), Is.EqualTo(10), "no item may appear on two pages");
            Assert.That(paged, Is.EquivalentTo(expected), "no item may be omitted");
        }
    }

    [Test]
    public async Task Paging_order_is_repeatable_when_raised_at_collides()
    {
        var sameInstant = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 6; i++)
        {
            await EventLogDataStore.Add(CreateLogItem($"Collision{i}", sameInstant));
        }

        await CompleteDatabaseOperation();

        var (first, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 1, pageSize: 6));
        var (second, _, _) = await EventLogDataStore.GetEventLogItems(new PagingInfo(page: 1, pageSize: 6));

        Assert.That(second.Select(i => i.Id), Is.EqualTo(first.Select(i => i.Id)).AsCollection);
    }

    // The count term in the version exists so that retention deleting the oldest rows invalidates a
    // client's cache. Nothing else verifies that, because IEventLogDataStore has no delete.
    [Test]
    public async Task Version_changes_when_rows_are_deleted_behind_the_interface()
    {
        var baseTime = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 3; i++)
        {
            await EventLogDataStore.Add(CreateLogItem($"Event{i}", baseTime.AddMinutes(i)));
        }

        await CompleteDatabaseOperation();

        var (_, totalBefore, versionBefore) = await EventLogDataStore.GetEventLogItems(new PagingInfo());
        Assert.That(totalBefore, Is.EqualTo(3));

        // Delete the OLDEST row — the one a retention sweep would take. A rowversion or MAX(RaisedAt)
        // scheme would be entirely blind to this; only the count term catches it.
        await DeleteOldest();

        var (_, totalAfter, versionAfter) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(totalAfter, Is.EqualTo(2));
            Assert.That(versionAfter, Is.Not.EqualTo(versionBefore), "a delete must invalidate the client's cached page");
        }
    }

    async Task DeleteOldest()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        var oldest = await dbContext.EventLogItems
            .OrderBy(e => e.RaisedAt)
            .ThenBy(e => e.Id)
            .FirstAsync(TestContext.CurrentContext.CancellationToken);

        dbContext.EventLogItems.Remove(oldest);
        await dbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
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
