namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Recoverability;
using ServiceControl.Recoverability.Archiving.Metrics;

/// <summary>
/// Instrument names and tag values are what dashboards and alerts are built on, so they are a
/// published contract and not an implementation detail.
/// </summary>
[TestFixture]
class ArchiveMetricsTests
{
    [SetUp]
    public void CreateMeterFactory()
    {
        provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
    }

    [TearDown]
    public void DisposeMeterFactory() => provider.Dispose();

    [Test]
    public void The_meter_publishes_the_instruments_it_is_named_for()
    {
        var published = new List<string>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == ArchiveMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, MeterFactory))
                {
                    published.Add(instrument.Name);
                }
            }
        };

        listener.Start();

        _ = new ArchiveMetrics(MeterFactory, fakeTime);

        Assert.That(published.Order(), Is.EqualTo(new[]
        {
            "sc.archive.batch_duration_seconds",
            "sc.archive.messages_total",
            "sc.archive.operation_duration_seconds",
            "sc.archive.operations_in_progress"
        }));
    }

    [Test]
    public async Task An_archive_operation_records_batch_gaps_messages_and_total_duration()
    {
        var metrics = new ArchiveMetrics(MeterFactory, fakeTime);
        using var recorded = new RecordedArchiveMetrics(MeterFactory);
        var archive = new InMemoryArchive("group-1", ArchiveType.FailureGroup, new FakeDomainEvents(), TimeProvider.System, metrics) { TotalNumberOfMessages = 1500 };

        await archive.Start();
        Assert.That(recorded.InProgress(ArchiveOperationKind.Archive, "started"), Is.EqualTo(1));

        fakeTime.Advance(TimeSpan.FromSeconds(2));
        await archive.BatchArchived(1000);
        Assert.That(recorded.InProgress(ArchiveOperationKind.Archive, "started"), Is.Zero);
        Assert.That(recorded.InProgress(ArchiveOperationKind.Archive, "progressing"), Is.EqualTo(1));

        fakeTime.Advance(TimeSpan.FromSeconds(1));
        await archive.BatchArchived(500);

        await archive.FinalizeArchive();
        Assert.That(recorded.InProgress(ArchiveOperationKind.Archive, "finalizing"), Is.EqualTo(1));

        await archive.Complete();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.BatchDurations(ArchiveOperationKind.Archive).Select(batch => batch.Value), Is.EqualTo(new[] { 2.0, 1.0 }));
            Assert.That(recorded.Messages(ArchiveOperationKind.Archive), Is.EqualTo(1500));
            Assert.That(recorded.OperationDurations(ArchiveOperationKind.Archive).Single().Value, Is.EqualTo(3.0));
            Assert.That(recorded.InProgress(ArchiveOperationKind.Archive, "started"), Is.Zero);
            Assert.That(recorded.InProgress(ArchiveOperationKind.Archive, "progressing"), Is.Zero);
            Assert.That(recorded.InProgress(ArchiveOperationKind.Archive, "finalizing"), Is.Zero);
        }
    }

    [Test]
    public async Task An_unarchive_operation_is_tagged_unarchive()
    {
        var metrics = new ArchiveMetrics(MeterFactory, fakeTime);
        using var recorded = new RecordedArchiveMetrics(MeterFactory);
        var unarchive = new InMemoryUnarchive("group-1", ArchiveType.FailureGroup, new FakeDomainEvents(), TimeProvider.System, metrics) { TotalNumberOfMessages = 200 };

        await unarchive.Start();
        fakeTime.Advance(TimeSpan.FromSeconds(5));
        await unarchive.BatchUnarchived(200);
        await unarchive.FinalizeUnarchive();
        await unarchive.Complete();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.BatchDurations(ArchiveOperationKind.Unarchive).Single().Value, Is.EqualTo(5.0));
            Assert.That(recorded.Messages(ArchiveOperationKind.Unarchive), Is.EqualTo(200));
            Assert.That(recorded.OperationDurations(ArchiveOperationKind.Unarchive).Single().Value, Is.EqualTo(5.0));
            Assert.That(recorded.BatchDurations(ArchiveOperationKind.Archive), Is.Empty);
        }
    }

    [Test]
    public async Task An_operation_that_never_completes_stays_visible_on_the_gauge()
    {
        var metrics = new ArchiveMetrics(MeterFactory, fakeTime);
        using var recorded = new RecordedArchiveMetrics(MeterFactory);
        var archive = new InMemoryArchive("group-stuck", ArchiveType.FailureGroup, new FakeDomainEvents(), TimeProvider.System, metrics) { TotalNumberOfMessages = 2000 };

        await archive.Start();
        await archive.BatchArchived(1000);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.InProgress(ArchiveOperationKind.Archive, "progressing"), Is.EqualTo(1));
            Assert.That(recorded.OperationDurations(ArchiveOperationKind.Archive), Is.Empty);
        }
    }

    [Test]
    public async Task A_restarted_operation_counts_once_on_the_gauge()
    {
        var metrics = new ArchiveMetrics(MeterFactory, fakeTime);
        using var recorded = new RecordedArchiveMetrics(MeterFactory);
        var archive = new InMemoryArchive("group-1", ArchiveType.FailureGroup, new FakeDomainEvents(), TimeProvider.System, metrics) { TotalNumberOfMessages = 10 };

        await archive.Start();
        await archive.BatchArchived(10);
        await archive.Complete();

        await archive.Start();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.InProgress(ArchiveOperationKind.Archive, "started"), Is.EqualTo(1));
            Assert.That(recorded.InProgress(ArchiveOperationKind.Archive, "progressing"), Is.Zero);
        }
    }

    [Test]
    public async Task Without_metrics_the_state_machine_runs_unchanged()
    {
        var archive = new InMemoryArchive("group-1", ArchiveType.FailureGroup, new FakeDomainEvents(), TimeProvider.System) { TotalNumberOfMessages = 10 };

        await archive.Start();
        await archive.BatchArchived(10);
        await archive.FinalizeArchive();
        await archive.Complete();

        Assert.That(archive.ArchiveState, Is.EqualTo(ArchiveState.ArchiveCompleted));
    }

    IMeterFactory MeterFactory => provider.GetRequiredService<IMeterFactory>();

    ServiceProvider provider;
    FakeTimeProvider fakeTime;
}
