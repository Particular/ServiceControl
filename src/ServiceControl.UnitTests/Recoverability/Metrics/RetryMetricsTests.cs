namespace ServiceControl.UnitTests.Recoverability.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;
    using ServiceControl.Recoverability.Retrying.Metrics;

    /// <summary>
    /// Instrument names and tag values are what dashboards and alerts are built on, so they are a
    /// published contract and not an implementation detail.
    /// </summary>
    [TestFixture]
    class RetryMetricsTests
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

            var metrics = new RetryMetrics(MeterFactory, TimeProvider.System);
            metrics.ObserveOperationsInProgress(() => []);
            metrics.ObservePendingBulkRequests(() => 0);

            Assert.That(published.Order(), Is.EqualTo(new[]
            {
                "sc.retry.forward_duration_seconds",
                "sc.retry.messages_total",
                "sc.retry.operation_duration_seconds",
                "sc.retry.operations_in_progress",
                "sc.retry.pending_bulk_requests",
                "sc.retry.prepare_duration_seconds",
                "sc.retry.stage_duration_seconds"
            }));
        }

        [Test]
        public void Every_retry_type_maps_to_its_own_tag_value()
        {
            var metrics = new RetryMetrics(MeterFactory, TimeProvider.System);
            using var recorded = new RecordedRetryMetrics(MeterFactory);

            foreach (var retryType in Enum.GetValues<RetryType>())
            {
                metrics.RecordMessages(retryType, RetryMessageOutcome.Staged, 1);
            }

            var tagValues = recorded.Of(RetryMetrics.MessagesInstrumentName).Select(measurement => measurement.Tags["retry.type"]);

            Assert.That(tagValues, Is.EquivalentTo(new[] { "unknown", "single", "group", "batch", "endpoint", "all", "queue" }));
        }

        [Test]
        public void Operation_completion_is_tagged_with_its_result()
        {
            var metrics = new RetryMetrics(MeterFactory, TimeProvider.System);
            using var recorded = new RecordedRetryMetrics(MeterFactory);

            metrics.RecordOperationCompleted(RetryType.FailureGroup, metrics.GetTimestamp(), failed: false);
            metrics.RecordOperationCompleted(RetryType.FailureGroup, metrics.GetTimestamp(), failed: true);

            var durations = recorded.Of(RetryMetrics.OperationDurationInstrumentName);

            Assert.That(durations, Has.Count.EqualTo(2));
            Assert.That(durations[0].Tags["result"], Is.EqualTo("success"));
            Assert.That(durations[0].Tags["retry.type"], Is.EqualTo("group"));
            Assert.That(durations[1].Tags["result"], Is.EqualTo("failed"));
        }

        [Test]
        public void A_scope_records_the_outcome_it_was_given()
        {
            var metrics = new RetryMetrics(MeterFactory, TimeProvider.System);
            using var recorded = new RecordedRetryMetrics(MeterFactory);

            using (var staging = metrics.BeginStaging(RetryType.FailureGroup))
            {
                staging.Complete();
            }

            using (var staging = metrics.BeginStaging(RetryType.FailureGroup))
            {
                staging.Empty();
            }

            using (metrics.BeginStaging(RetryType.FailureGroup))
            {
            }

            var results = recorded.Of(RetryMetrics.StageDurationInstrumentName).Select(measurement => measurement.Tags["result"]);

            Assert.That(results, Is.EqualTo(new[] { "success", "empty", "failed" }));
        }

        [Test]
        public void A_scope_cut_short_by_shutdown_is_recorded_as_cancelled()
        {
            var metrics = new RetryMetrics(MeterFactory, TimeProvider.System);
            using var recorded = new RecordedRetryMetrics(MeterFactory);
            using var shutdown = new CancellationTokenSource();

            using (metrics.BeginPreparation(RetryType.All, shutdown.Token))
            {
                shutdown.Cancel();
            }

            Assert.That(recorded.Of(RetryMetrics.PrepareDurationInstrumentName).Single().Tags["result"], Is.EqualTo("cancelled"));
        }

        [Test]
        public void A_scope_that_finished_before_shutdown_is_still_a_success()
        {
            var metrics = new RetryMetrics(MeterFactory, TimeProvider.System);
            using var recorded = new RecordedRetryMetrics(MeterFactory);
            using var shutdown = new CancellationTokenSource();

            using (var preparation = metrics.BeginPreparation(RetryType.All, shutdown.Token))
            {
                preparation.Complete();
                shutdown.Cancel();
            }

            Assert.That(recorded.Of(RetryMetrics.PrepareDurationInstrumentName).Single().Tags["result"], Is.EqualTo("success"));
        }

        [Test]
        public void Forwarding_is_tagged_with_its_mode()
        {
            var metrics = new RetryMetrics(MeterFactory, TimeProvider.System);
            using var recorded = new RecordedRetryMetrics(MeterFactory);

            using (var forwarding = metrics.BeginForwarding(RetryType.FailureGroup, recoveringFromPrematureShutdown: false))
            {
                forwarding.Complete();
            }

            using (metrics.BeginForwarding(RetryType.FailureGroup, recoveringFromPrematureShutdown: true))
            {
            }

            var forwarded = recorded.Of(RetryMetrics.ForwardDurationInstrumentName);

            Assert.That(forwarded.Select(measurement => measurement.Tags["mode"]), Is.EqualTo(new[] { "counting", "timeout" }));
            Assert.That(forwarded.Select(measurement => measurement.Tags["result"]), Is.EqualTo(new[] { "success", "failed" }));
        }

        [Test]
        public void The_gauge_counts_operations_by_type_and_state_and_excludes_completed()
        {
            var metrics = new RetryMetrics(MeterFactory, TimeProvider.System);
            using var recorded = new RecordedRetryMetrics(MeterFactory);

            metrics.ObserveOperationsInProgress(() =>
            [
                (RetryType.FailureGroup, RetryState.Preparing),
                (RetryType.FailureGroup, RetryState.Preparing),
                (RetryType.SingleMessage, RetryState.Forwarding),
                (RetryType.All, RetryState.Waiting),
                (RetryType.All, RetryState.Completed)
            ]);

            var observed = recorded.Observe(RetryMetrics.OperationsInProgressInstrumentName)
                .ToDictionary(measurement => (measurement.Tags["retry.type"], measurement.Tags["retry.state"]), measurement => measurement.Value);

            Assert.That(observed, Is.EqualTo(new Dictionary<(object, object), double>
            {
                { ("group", "preparing"), 2 },
                { ("single", "forwarding"), 1 },
                { ("all", "waiting"), 1 }
            }));
        }

        [Test]
        public void The_bulk_request_gauge_reads_the_queue_depth()
        {
            var metrics = new RetryMetrics(MeterFactory, TimeProvider.System);
            using var recorded = new RecordedRetryMetrics(MeterFactory);

            var depth = 3;
            metrics.ObservePendingBulkRequests(() => depth);

            Assert.That(recorded.Observe(RetryMetrics.PendingBulkRequestsInstrumentName).Single().Value, Is.EqualTo(3));

            depth = 0;

            Assert.That(recorded.Observe(RetryMetrics.PendingBulkRequestsInstrumentName).Single().Value, Is.Zero);
        }

        // Every fixture in the run shares the meter name, so the factory is what tells the instruments
        // created here apart from the ones another test left behind.
        bool BelongsToThisTest(Instrument instrument) =>
            instrument.Meter.Name == RetryMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, MeterFactory);

        IMeterFactory MeterFactory => provider.GetRequiredService<IMeterFactory>();

        ServiceProvider provider;
    }
}
