namespace ServiceControl.UnitTests.Recoverability;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;
using ServiceControl.UnitTests.Operations;

[TestFixture]
public class OperationProgressClockTests
{
    static readonly DateTime FixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    [Test]
    public async Task Archive_progress_and_completion_come_from_the_injected_clock()
    {
        var archive = new InMemoryArchive("group-1", ArchiveType.FailureGroup, new FakeDomainEvents(), new FixedClock(FixedNow));

        await archive.BatchArchived(1);
        Assert.That(archive.Last, Is.EqualTo(FixedNow));

        await archive.FinalizeArchive();
        Assert.That(archive.Last, Is.EqualTo(FixedNow));

        await archive.Complete();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(archive.CompletionTime, Is.EqualTo(FixedNow));
            Assert.That(archive.Last, Is.EqualTo(FixedNow));
        }
    }

    [Test]
    public async Task Unarchive_progress_and_completion_come_from_the_injected_clock()
    {
        var unarchive = new InMemoryUnarchive("group-1", ArchiveType.FailureGroup, new FakeDomainEvents(), new FixedClock(FixedNow));

        await unarchive.BatchUnarchived(1);
        Assert.That(unarchive.Last, Is.EqualTo(FixedNow));

        await unarchive.FinalizeUnarchive();
        Assert.That(unarchive.Last, Is.EqualTo(FixedNow));

        await unarchive.Complete();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(unarchive.CompletionTime, Is.EqualTo(FixedNow));
            Assert.That(unarchive.Last, Is.EqualTo(FixedNow));
        }
    }

    [Test]
    public async Task Completing_an_archive_reads_the_clock_once()
    {
        var archive = new InMemoryArchive("group-1", ArchiveType.FailureGroup, new FakeDomainEvents(), new TickingClock(FixedNow));

        await archive.Complete();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(archive.CompletionTime, Is.EqualTo(FixedNow));
            Assert.That(archive.Last, Is.EqualTo(FixedNow));
        }
    }

    [Test]
    public async Task Completing_an_unarchive_reads_the_clock_once()
    {
        var unarchive = new InMemoryUnarchive("group-1", ArchiveType.FailureGroup, new FakeDomainEvents(), new TickingClock(FixedNow));

        await unarchive.Complete();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unarchive.CompletionTime, Is.EqualTo(FixedNow));
            Assert.That(unarchive.Last, Is.EqualTo(FixedNow));
        }
    }

    [Test]
    public async Task Retry_completion_comes_from_the_injected_clock()
    {
        var retry = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(new FixedClock(FixedNow)), NullLogger.Instance, new FixedClock(FixedNow));

        await retry.Prepare(1000);
        await retry.PrepareBatch(1000);
        await retry.Forwarding();
        await retry.BatchForwarded(1000);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(retry.RetryState, Is.EqualTo(RetryState.Completed));
            Assert.That(retry.CompletionTime, Is.EqualTo(FixedNow));
        }
    }

    sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    // Moves on every read, so a member that reads twice cannot get the same value twice.
    sealed class TickingClock(DateTime start) : TimeProvider
    {
        DateTime next = start;

        public override DateTimeOffset GetUtcNow()
        {
            var value = next;
            next = next.AddSeconds(1);
            return new DateTimeOffset(value, TimeSpan.Zero);
        }
    }
}
