#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationBodyLoaderTests
{
    static readonly MigrationCategory Category = MigrationCategoryRegistry.Find(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages)!;
    static readonly MigrationBody Body = new(Encoding.UTF8.GetBytes("<order>1</order>"), "text/xml");

    [Test]
    public async Task Attaches_the_body_to_every_row()
    {
        var source = SourceWithBodies("FailedMessages/one", "FailedMessages/two");

        var result = await Load(source, "FailedMessages/one", "FailedMessages/two");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Rows.Select(row => row.SourceId), Is.EqualTo(new[] { "FailedMessages/one", "FailedMessages/two" }));
            Assert.That(result.Rows.Select(row => row.Body), Is.All.SameAs(Body));
            Assert.That(result.Skipped, Is.Empty);
        }
    }

    [Test]
    public async Task Retries_a_failing_read_three_times_then_skips_the_message_as_BodyUnreadable()
    {
        var source = SourceWithBodies("FailedMessages/one", "FailedMessages/two");
        source.FailBodyReads("FailedMessages/two", MigrationEngine.MaxBodyReadAttempts, new TimeoutException("body store unreachable"));

        var result = await Load(source, "FailedMessages/one", "FailedMessages/two");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.BodyReadAttempts("FailedMessages/two"), Is.EqualTo(MigrationEngine.MaxBodyReadAttempts));
            Assert.That(result.Rows.Select(row => row.SourceId), Is.EqualTo(new[] { "FailedMessages/one" }), "a message with no body is a retry hazard, so it is left out rather than written without one");
            Assert.That(result.Skipped, Is.EqualTo(new[] { ("FailedMessages/two", MigrationSkipReason.BodyUnreadable) }));
        }
    }

    [Test]
    public async Task Keeps_a_message_whose_read_succeeds_on_the_last_attempt()
    {
        var source = SourceWithBodies("FailedMessages/one");
        source.FailBodyReads("FailedMessages/one", MigrationEngine.MaxBodyReadAttempts - 1, new TimeoutException("blip"));

        var result = await Load(source, "FailedMessages/one");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Rows.Single().Body, Is.SameAs(Body));
            Assert.That(result.Skipped, Is.Empty);
        }
    }

    [Test]
    public async Task Skips_a_message_the_source_has_no_body_for_as_BodyMissing_after_one_read()
    {
        var source = new InMemoryMigrationSource();

        var result = await Load(source, "FailedMessages/one");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.BodyReadAttempts("FailedMessages/one"), Is.EqualTo(1), "a read that found nothing learns nothing by repeating");
            Assert.That(result.Rows, Is.Empty);
            Assert.That(result.Skipped, Is.EqualTo(new[] { ("FailedMessages/one", MigrationSkipReason.BodyMissing) }),
                "a missing body is not an unreadable one, and only a separate reason lets the refusal say no retry fixes it");
        }
    }

    [Test]
    public void Lets_a_defect_out_on_the_first_attempt_rather_than_skipping_the_message()
    {
        var source = SourceWithBodies("FailedMessages/one");
        source.FailBodyReads("FailedMessages/one", MigrationEngine.MaxBodyReadAttempts, new NotSupportedException("this source cannot read bodies"));

        Assert.ThrowsAsync<NotSupportedException>(() => Load(source, "FailedMessages/one"),
            "a defect fails the same way on every attempt, so retrying it and skipping the message would blame the body store for a bug");
        Assert.That(source.BodyReadAttempts("FailedMessages/one"), Is.EqualTo(1));
    }

    [Test]
    public void Logs_a_defect_with_the_message_it_was_reading_before_letting_it_out()
    {
        var source = SourceWithBodies("FailedMessages/one");
        var defect = new NotSupportedException("this source cannot read bodies");
        source.FailBodyReads("FailedMessages/one", 1, defect);
        var logger = new CapturingLogger();
        var batch = new MigrationBatch([new MigrationRow("FailedMessages/one", new object(), new Dictionary<string, object?>())], "FailedMessages/one");

        Assert.ThrowsAsync<NotSupportedException>(() => new MigrationBodyLoader(source, new FakeTimeProvider(), TimeSpan.Zero, logger).Load(Category, batch, TestContext.CurrentContext.CancellationToken));

        var error = logger.Entries.Single(entry => entry.Level == LogLevel.Error);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.Message, Does.Contain("FailedMessages/one").And.Contain(Category.Id), "the exception that halts the category does not say which message it was reading, so this line is the only record of it");
            Assert.That(error.Exception, Is.SameAs(defect));
        }
    }

    [Test]
    public async Task Retries_a_cancellation_the_run_did_not_ask_for_and_then_skips_the_message_as_BodyUnreadable()
    {
        var source = SourceWithBodies("FailedMessages/one");
        source.FailBodyReads("FailedMessages/one", MigrationEngine.MaxBodyReadAttempts, new OperationCanceledException("the client timed out"));

        var result = await Load(source, "FailedMessages/one");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.BodyReadAttempts("FailedMessages/one"), Is.EqualTo(MigrationEngine.MaxBodyReadAttempts), "a client timeout raised as a cancellation is a transient failure, not a shutdown");
            Assert.That(result.Skipped, Is.EqualTo(new[] { ("FailedMessages/one", MigrationSkipReason.BodyUnreadable) }));
        }
    }

    [Test]
    public async Task Keeps_a_body_the_source_attached_and_never_reads_it_again()
    {
        var source = SourceWithBodies("FailedMessages/two");
        var attached = new MigrationBody(Encoding.UTF8.GetBytes("attached in Read"), "text/plain");

        var batch = new MigrationBatch(
            [
                new MigrationRow("FailedMessages/one", new object(), new Dictionary<string, object?>(), attached),
                new MigrationRow("FailedMessages/two", new object(), new Dictionary<string, object?>())
            ],
            "FailedMessages/two");

        var result = await Loader(source).Load(Category, batch, TestContext.CurrentContext.CancellationToken);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.BodyReadAttempts("FailedMessages/one"), Is.Zero, "a source that attaches bodies in Read would otherwise pay a second read per message for data it already had");
            Assert.That(result.Rows[0].Body, Is.SameAs(attached));
            Assert.That(result.Rows[1].Body, Is.SameAs(Body));
        }
    }

    [Test]
    public async Task Fetches_at_most_eight_bodies_at_a_time_and_keeps_the_batch_order()
    {
        var sourceIds = Enumerable.Range(0, 40).Select(index => $"FailedMessages/{index}").ToArray();
        var source = SourceWithBodies(sourceIds);
        source.HoldBodyReads();

        var load = Load(source, sourceIds);

        try
        {
            using var waitLimit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await source.BodyReadsInFlight(MigrationBodyLoader.MaxConcurrentFetches, waitLimit.Token);
            // Gives an unbounded loader time to open the rest of the forty reads, which it would do all at once.
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            Assert.That(source.PeakBodyReadsInFlight, Is.EqualTo(MigrationBodyLoader.MaxConcurrentFetches),
                "an unbounded loader opens all forty reads at once, which turns the copier into a load spike on a live source");
        }
        finally
        {
            source.ReleaseBodyReads();
        }

        var result = await load.WaitAsync(TimeSpan.FromSeconds(5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Rows.Select(row => row.SourceId), Is.EqualTo(sourceIds), "the batch's order is the cursor's order, so it has to survive being fetched out of order");
            Assert.That(result.Skipped, Is.Empty);
            Assert.That(source.PeakBodyReadsInFlight, Is.EqualTo(MigrationBodyLoader.MaxConcurrentFetches));
        }
    }

    static InMemoryMigrationSource SourceWithBodies(params string[] sourceIds)
    {
        var source = new InMemoryMigrationSource();

        foreach (var sourceId in sourceIds)
        {
            source.SetBody(sourceId, Body);
        }

        return source;
    }

    // Zero backoff because the FakeTimeProvider is never advanced, and a wait on it would never finish.
    static MigrationBodyLoader Loader(InMemoryMigrationSource source) =>
        new(source, new FakeTimeProvider(), TimeSpan.Zero, NullLogger.Instance);

    static Task<MigrationBodyLoadResult> Load(InMemoryMigrationSource source, params string[] sourceIds)
    {
        var batch = new MigrationBatch(
            [.. sourceIds.Select(sourceId => new MigrationRow(sourceId, new object(), new Dictionary<string, object?>()))],
            sourceIds[^1]);

        return Loader(source).Load(Category, batch, TestContext.CurrentContext.CancellationToken);
    }
}
