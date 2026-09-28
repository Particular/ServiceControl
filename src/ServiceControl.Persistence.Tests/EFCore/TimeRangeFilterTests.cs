namespace ServiceControl.Persistence.Tests;

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.Infrastructure;

class TimeRangeFilterTests : ErrorIngestionTestBase
{
    [Test]
    public async Task Filters_by_a_time_sent_range_with_an_offset()
    {
        var before = new IngestedFailure { TimeSent = new DateTime(2026, 7, 22, 9, 58, 0, DateTimeKind.Utc) };
        var inside = new IngestedFailure { TimeSent = new DateTime(2026, 7, 22, 10, 1, 0, DateTimeKind.Utc) };
        var after = new IngestedFailure { TimeSent = new DateTime(2026, 7, 22, 10, 4, 0, DateTimeKind.Utc) };

        await Ingest(before, inside, after);

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), true,
            new DateTimeRange("2026-07-22T12:00:00+02:00", "2026-07-22T12:03:00+02:00"));

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { inside.UniqueMessageIdString }));
    }

    [Test]
    public async Task Filters_by_a_modified_range_with_an_offset()
    {
        var (inside, from, to) = await IngestThreeModifiedTwoMinutesApart();

        var result = await FailedMessageQueryStore.GetFailedMessages(null, $"{WithOffset(from)}...{WithOffset(to)}", null, new PagingInfo(), new SortInfo());

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { inside }));
    }

    [Test]
    public async Task Filters_by_a_modified_range_without_a_zone_as_utc()
    {
        var (inside, from, to) = await IngestThreeModifiedTwoMinutesApart();

        var result = await FailedMessageQueryStore.GetFailedMessages(null, $"{WithoutZone(from)}...{WithoutZone(to)}", null, new PagingInfo(), new SortInfo());

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { inside }));
    }

    async Task<(string Inside, DateTime From, DateTime To)> IngestThreeModifiedTwoMinutesApart()
    {
        var start = Now;
        var inside = new IngestedFailure();

        await Ingest(new IngestedFailure());
        AdvanceClock(TimeSpan.FromMinutes(2));
        await Ingest(inside);
        AdvanceClock(TimeSpan.FromMinutes(2));
        await Ingest(new IngestedFailure());

        return (inside.UniqueMessageIdString, start.AddMinutes(1), start.AddMinutes(3));
    }

    static string WithOffset(DateTime utc) =>
        new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture);

    static string WithoutZone(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture);
}
