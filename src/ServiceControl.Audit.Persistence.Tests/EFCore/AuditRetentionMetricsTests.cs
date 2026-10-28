namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure.Metrics;

    /// <summary>
    /// Instrument names are what dashboards and alerts are built on, so they are a published contract.
    /// They match the error instance's retention instruments on purpose, so the same retention queries and alerts cover both instances, told apart by job.
    /// The audit instance has no rows deleted counter, because PostgreSQL drops whole partitions and a dropped partition reports no row count.
    /// </summary>
    [TestFixture]
    class AuditRetentionMetricsTests
    {
        [SetUp]
        public void CreateMeterFactory() => provider = new ServiceCollection().AddMetrics().BuildServiceProvider();

        [TearDown]
        public void DisposeMeterFactory() => provider.Dispose();

        [Test]
        public void The_meter_publishes_the_error_instance_instruments_other_than_rows_deleted()
        {
            var published = new List<Instrument>();

            using var listener = new MeterListener
            {
                InstrumentPublished = (instrument, _) =>
                {
                    if (BelongsToThisTest(instrument))
                    {
                        published.Add(instrument);
                    }
                }
            };

            listener.Start();

            _ = new AuditRetentionMetrics(MeterFactory);

            var cycleDuration = (Histogram<double>)published.Single(instrument => instrument.Name == "sc.retention.cycle_duration_seconds");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(published.Select(instrument => instrument.Name).Order(), Is.EqualTo(new[]
                {
                    "sc.retention.consecutive_failures_total",
                    "sc.retention.cycle_duration_seconds"
                }));
                Assert.That(published.Select(instrument => instrument.Meter.Name).Distinct(), Is.EqualTo(new[] { "Particular.ServiceControl.Audit" }));
                Assert.That(cycleDuration.Advice?.HistogramBucketBoundaries, Is.EqualTo(new[] { 0.1, 0.5, 1, 5, 15, 60, 300, 900 }));
                Assert.That(cycleDuration.Unit, Is.EqualTo("seconds"));
                Assert.That(published.Single(instrument => instrument.Name == "sc.retention.consecutive_failures_total"), Is.InstanceOf<ObservableGauge<long>>());
            }
        }

        [Test]
        public void A_completed_cycle_is_recorded_as_a_success()
        {
            var metrics = new AuditRetentionMetrics(MeterFactory);
            using var recorded = Listen();

            using (var cycle = metrics.BeginCycle(AuditRetentionEntity.SagaSnapshots))
            {
                cycle.Complete();
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.CycleResults(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(new[] { "success" }));
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.SagaSnapshots), Is.Zero);
            }
        }

        [Test]
        public void A_cycle_records_its_duration_in_seconds()
        {
            var metrics = new AuditRetentionMetrics(MeterFactory);
            using var recorded = Listen();

            using (var cycle = metrics.BeginCycle(AuditRetentionEntity.AuditMessages))
            {
                Thread.Sleep(50);
                cycle.Complete();
            }

            Assert.That(recorded.Cycles(AuditRetentionEntity.AuditMessages)[0].Value, Is.InRange(0.04, 10));
        }

        [Test]
        public void Every_kind_of_row_is_covered_by_a_tag()
        {
            var metrics = new AuditRetentionMetrics(MeterFactory);

            Assert.That(() =>
            {
                foreach (var entity in System.Enum.GetValues<AuditRetentionEntity>())
                {
                    metrics.BeginCycle(entity).Dispose();
                }
            }, Throws.Nothing);
        }

        [Test]
        public void An_abandoned_cycle_is_recorded_as_a_failure()
        {
            var metrics = new AuditRetentionMetrics(MeterFactory);
            using var recorded = Listen();

            metrics.BeginCycle(AuditRetentionEntity.SagaSnapshots).Dispose();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.CycleResults(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(new[] { "failed" }));
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(1));
            }
        }

        [Test]
        public void Consecutive_failures_are_counted_per_entity()
        {
            var metrics = new AuditRetentionMetrics(MeterFactory);
            using var recorded = Listen();

            metrics.BeginCycle(AuditRetentionEntity.AuditMessages).Dispose();
            metrics.BeginCycle(AuditRetentionEntity.AuditMessages).Dispose();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.AuditMessages), Is.EqualTo(2));
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.SagaSnapshots), Is.Zero);
            }
        }

        [Test]
        public void A_success_clears_the_failures_of_that_entity_alone()
        {
            var metrics = new AuditRetentionMetrics(MeterFactory);
            using var recorded = Listen();

            metrics.BeginCycle(AuditRetentionEntity.AuditMessages).Dispose();
            metrics.BeginCycle(AuditRetentionEntity.SagaSnapshots).Dispose();

            using (var cycle = metrics.BeginCycle(AuditRetentionEntity.AuditMessages))
            {
                cycle.Complete();
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.AuditMessages), Is.Zero);
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.SagaSnapshots), Is.EqualTo(1));
            }
        }

        [Test]
        public void A_cycle_interrupted_by_shutdown_is_recorded_as_cancelled()
        {
            var metrics = new AuditRetentionMetrics(MeterFactory);
            using var recorded = Listen();
            using var shutdown = new CancellationTokenSource();

            using (metrics.BeginCycle(AuditRetentionEntity.AuditMessages, shutdown.Token))
            {
                shutdown.Cancel();
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(recorded.CycleResults(AuditRetentionEntity.AuditMessages), Is.EqualTo(new[] { "cancelled" }));
                Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.AuditMessages), Is.Zero);
            }
        }

        [Test]
        public void A_cycle_that_finished_before_shutdown_is_still_a_success()
        {
            var metrics = new AuditRetentionMetrics(MeterFactory);
            using var recorded = Listen();
            using var shutdown = new CancellationTokenSource();

            using (var cycle = metrics.BeginCycle(AuditRetentionEntity.AuditMessages, shutdown.Token))
            {
                cycle.Complete();
                shutdown.Cancel();
            }

            Assert.That(recorded.CycleResults(AuditRetentionEntity.AuditMessages), Is.EqualTo(new[] { "success" }));
        }

        [Test]
        public void Shutdown_neither_clears_nor_adds_to_the_failures()
        {
            var metrics = new AuditRetentionMetrics(MeterFactory);
            using var recorded = Listen();
            using var shutdown = new CancellationTokenSource();

            metrics.BeginCycle(AuditRetentionEntity.AuditMessages).Dispose();

            shutdown.Cancel();
            metrics.BeginCycle(AuditRetentionEntity.AuditMessages, shutdown.Token).Dispose();

            Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.AuditMessages), Is.EqualTo(1));
        }

        [Test]
        public void Concurrent_failures_are_all_counted()
        {
            var metrics = new AuditRetentionMetrics(MeterFactory);
            using var recorded = Listen();

            const int failedCycles = 1000;

            Parallel.For(0, failedCycles, _ => metrics.BeginCycle(AuditRetentionEntity.AuditMessages).Dispose());

            Assert.That(recorded.ConsecutiveFailures(AuditRetentionEntity.AuditMessages), Is.EqualTo(failedCycles));
        }

        RecordedAuditRetentionMetrics Listen() => new(MeterFactory);

        // Every fixture in the run shares the meter name, so the factory is what tells the instruments
        // created here apart from the ones another test left behind.
        bool BelongsToThisTest(Instrument instrument) =>
            instrument.Meter.Name == AuditRetentionMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, MeterFactory);

        IMeterFactory MeterFactory => provider.GetRequiredService<IMeterFactory>();

        ServiceProvider provider;
    }
}
