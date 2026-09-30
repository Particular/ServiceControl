namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.Infrastructure;

class TimeRangeFilterTests : ErrorIngestionTestBase
{
    [Test]
    public async Task Filters_by_a_time_sent_range_with_an_offset()
    {
        var inside = await IngestThreeSentThreeMinutesApart();

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), true,
            new DateTimeRange("2026-07-22T12:00:00+02:00", "2026-07-22T12:03:00+02:00"));

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { inside }));
    }

    [Test]
    public async Task Filters_by_a_time_sent_range_without_a_zone_as_utc()
    {
        var inside = await IngestThreeSentThreeMinutesApart();

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), true,
            new DateTimeRange("2026-07-22T10:00:00", "2026-07-22T10:03:00"));

        Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { inside }));
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

    [Test]
    public async Task Unarchives_by_a_modified_range_with_an_offset()
    {
        var (inside, from, to) = await IngestThreeModifiedTwoMinutesApart();
        await SetEveryStatus(FailedMessageStatus.Archived);

        var unarchived = await FailedMessageLifecycleStore.UnArchiveMessagesByRange(ParsedWithOffset(from), ParsedWithOffset(to));

        Assert.That(unarchived, Is.EqualTo(new[] { inside }));
    }

    [Test]
    public async Task Finds_pending_retries_by_a_modified_range_with_an_offset()
    {
        var (inside, from, to) = await IngestThreeModifiedTwoMinutesApart();
        await SetEveryStatus(FailedMessageStatus.RetryIssued);

        var pending = await FailedMessageRetryStore.GetRetryPendingMessages(ParsedWithOffset(from), ParsedWithOffset(to), "error");

        Assert.That(pending, Is.EqualTo(new[] { inside }).IgnoreCase);
    }

    [Test]
    public async Task Processes_pending_retries_by_a_modified_range_with_an_offset()
    {
        var (inside, from, to) = await IngestThreeModifiedTwoMinutesApart();
        await SetEveryStatus(FailedMessageStatus.RetryIssued);

        var processed = new List<string>();
        await FailedMessageRetryStore.ProcessPendingRetries(ParsedWithOffset(from), ParsedWithOffset(to), null, (id, _) =>
        {
            processed.Add(id);
            return Task.CompletedTask;
        });

        Assert.That(processed, Is.EqualTo(new[] { inside }).IgnoreCase);
    }

    async Task<string> IngestThreeSentThreeMinutesApart()
    {
        var inside = new IngestedFailure { TimeSent = new DateTime(2026, 7, 22, 10, 1, 0, DateTimeKind.Utc) };

        await Ingest(
            new IngestedFailure { TimeSent = new DateTime(2026, 7, 22, 9, 58, 0, DateTimeKind.Utc) },
            inside,
            new IngestedFailure { TimeSent = new DateTime(2026, 7, 22, 10, 4, 0, DateTimeKind.Utc) });

        return inside.UniqueMessageIdString;
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

    Task SetEveryStatus(FailedMessageStatus status) =>
        Query(dbContext => dbContext.FailedMessages.ExecuteUpdateAsync(setters => setters.SetProperty(message => message.Status, status)));

    static string WithOffset(DateTime utc) =>
        new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(2)).ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture);

    static string WithoutZone(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture);

    static DateTime ParsedWithOffset(DateTime utc) => DateTime.Parse(WithOffset(utc), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
