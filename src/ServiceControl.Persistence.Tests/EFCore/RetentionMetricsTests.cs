namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Infrastructure.Metrics;

/// <summary>
/// Instrument names are what dashboards and alerts are built on, so they are a published contract
/// and not an implementation detail.
/// </summary>
[TestFixture]
class RetentionMetricsTests
{
    [SetUp]
    public void CreateMeterFactory() => provider = new ServiceCollection().AddMetrics().BuildServiceProvider();

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
                if (BelongsToThisTest(instrument))
                {
                    published.Add(instrument.Name);
                }
            }
        };

        listener.Start();

        _ = new RetentionMetrics(MeterFactory);

        Assert.That(published.Order(), Is.EqualTo(new[]
        {
            "sc.retention.consecutive_failures_total",
            "sc.retention.cycle_duration_seconds",
            "sc.retention.rows_deleted_total"
        }));
    }

    [Test]
    public void A_completed_cycle_is_recorded_as_a_success()
    {
        var metrics = new RetentionMetrics(MeterFactory);
        using var recorded = Listen();

        using (var cycle = metrics.BeginCycle(RetentionEntity.EventLog))
        {
            cycle.Complete();
        }

        var cycles = recorded.Cycles(RetentionEntity.EventLog);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cycles, Has.Count.EqualTo(1));
            Assert.That(cycles[0].Result, Is.EqualTo("success"));
            Assert.That(recorded.ConsecutiveFailures(RetentionEntity.EventLog), Is.Zero);
        }
    }

    [Test]
    public void An_abandoned_cycle_is_recorded_as_a_failure()
    {
        var metrics = new RetentionMetrics(MeterFactory);
        using var recorded = Listen();

        metrics.BeginCycle(RetentionEntity.EventLog).Dispose();

        var cycles = recorded.Cycles(RetentionEntity.EventLog);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cycles, Has.Count.EqualTo(1));
            Assert.That(cycles[0].Result, Is.EqualTo("failed"));
            Assert.That(recorded.ConsecutiveFailures(RetentionEntity.EventLog), Is.EqualTo(1));
        }
    }

    [Test]
    public void Consecutive_failures_are_counted_per_entity()
    {
        var metrics = new RetentionMetrics(MeterFactory);
        using var recorded = Listen();

        metrics.BeginCycle(RetentionEntity.FailedMessages).Dispose();
        metrics.BeginCycle(RetentionEntity.FailedMessages).Dispose();
        metrics.BeginCycle(RetentionEntity.EventLog).Dispose();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.ConsecutiveFailures(RetentionEntity.FailedMessages), Is.EqualTo(2));
            Assert.That(recorded.ConsecutiveFailures(RetentionEntity.EventLog), Is.EqualTo(1));
            Assert.That(recorded.ConsecutiveFailures(RetentionEntity.GroupComments), Is.Zero);
        }
    }

    [Test]
    public void A_success_clears_the_failures_of_that_entity_alone()
    {
        var metrics = new RetentionMetrics(MeterFactory);
        using var recorded = Listen();

        metrics.BeginCycle(RetentionEntity.FailedMessages).Dispose();
        metrics.BeginCycle(RetentionEntity.EventLog).Dispose();

        using (var cycle = metrics.BeginCycle(RetentionEntity.FailedMessages))
        {
            cycle.Complete();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.ConsecutiveFailures(RetentionEntity.FailedMessages), Is.Zero);
            Assert.That(recorded.ConsecutiveFailures(RetentionEntity.EventLog), Is.EqualTo(1));
        }
    }

    [Test]
    public void A_cycle_interrupted_by_shutdown_is_not_recorded()
    {
        var metrics = new RetentionMetrics(MeterFactory);
        using var recorded = Listen();
        using var shutdown = new CancellationTokenSource();

        using (metrics.BeginCycle(RetentionEntity.FailedMessages, shutdown.Token))
        {
            shutdown.Cancel();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.Cycles(RetentionEntity.FailedMessages), Is.Empty);
            Assert.That(recorded.ConsecutiveFailures(RetentionEntity.FailedMessages), Is.Zero);
        }
    }

    [Test]
    public void Deleted_rows_are_counted_per_entity()
    {
        var metrics = new RetentionMetrics(MeterFactory);
        using var recorded = Listen();

        metrics.RecordRowsDeleted(RetentionEntity.FailedMessages, 1000);
        metrics.RecordRowsDeleted(RetentionEntity.FailedMessages, 7);
        metrics.RecordRowsDeleted(RetentionEntity.GroupComments, 3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recorded.RowsDeleted(RetentionEntity.FailedMessages), Is.EqualTo(1007));
            Assert.That(recorded.RowsDeleted(RetentionEntity.GroupComments), Is.EqualTo(3));
        }
    }

    [Test]
    public void Concurrent_failures_are_all_counted()
    {
        var metrics = new RetentionMetrics(MeterFactory);
        using var recorded = Listen();

        const int failedCycles = 1000;

        Parallel.For(0, failedCycles, _ => metrics.BeginCycle(RetentionEntity.FailedMessages).Dispose());

        Assert.That(recorded.ConsecutiveFailures(RetentionEntity.FailedMessages), Is.EqualTo(failedCycles));
    }

    RecordedRetentionMetrics Listen() => new(MeterFactory);

    // Every fixture in the run shares the meter name, so the factory is what tells the instruments
    // created here apart from the ones another test left behind.
    bool BelongsToThisTest(Instrument instrument) =>
        instrument.Meter.Name == RetentionMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, MeterFactory);

    IMeterFactory MeterFactory => provider.GetRequiredService<IMeterFactory>();

    ServiceProvider provider;
}
