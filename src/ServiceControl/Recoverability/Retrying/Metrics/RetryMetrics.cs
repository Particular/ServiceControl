namespace ServiceControl.Recoverability.Retrying.Metrics;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence;

public class RetryMetrics
{
    public const string MeterName = ServiceControlMeters.Error;

    public static readonly string OperationDurationInstrumentName = $"{InstrumentPrefix}.operation_duration_seconds";
    public static readonly string PrepareDurationInstrumentName = $"{InstrumentPrefix}.prepare_duration_seconds";
    public static readonly string StageDurationInstrumentName = $"{InstrumentPrefix}.stage_duration_seconds";
    public static readonly string ForwardDurationInstrumentName = $"{InstrumentPrefix}.forward_duration_seconds";
    public static readonly string MessagesInstrumentName = $"{InstrumentPrefix}.messages_total";
    public static readonly string OperationsInProgressInstrumentName = $"{InstrumentPrefix}.operations_in_progress";
    public static readonly string PendingBulkRequestsInstrumentName = $"{InstrumentPrefix}.pending_bulk_requests";

    public RetryMetrics(IMeterFactory meterFactory, TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
        meter = meterFactory.Create(MeterName, MeterVersion);

        operationDuration = meter.CreateHistogram(
            OperationDurationInstrumentName,
            unit: "seconds",
            description: "Retry operation duration from request to completion in seconds",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [1, 5, 15, 60, 300, 900, 1800, 3600] });

        prepareDuration = meter.CreateHistogram(
            PrepareDurationInstrumentName,
            unit: "seconds",
            description: "Retry preparation duration in seconds, covering the store scan and batch creation",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.1, 0.5, 1, 5, 15, 60, 300, 900] });

        stageDuration = meter.CreateHistogram(
            StageDurationInstrumentName,
            unit: "seconds",
            description: "Retry batch staging duration in seconds",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.05, 0.1, 0.5, 1, 5, 15, 60] });

        forwardDuration = meter.CreateHistogram(
            ForwardDurationInstrumentName,
            unit: "seconds",
            description: "Retry batch forwarding duration in seconds",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.5, 1, 5, 15, 45, 60, 300, 900] });

        messages = meter.CreateCounter<long>(MessagesInstrumentName, description: "Messages moved through the retry pipeline");
    }

    public long GetTimestamp() => timeProvider.GetTimestamp();

    public void RecordOperationCompleted(RetryType retryType, long startTimestamp, bool failed)
    {
        var tags = RetryTypeTags(retryType);
        tags.Add("result", failed ? "failed" : "success");

        operationDuration.Record(timeProvider.GetElapsedTime(startTimestamp).TotalSeconds, tags);
    }

    public void RecordMessages(RetryType retryType, RetryMessageOutcome outcome, long count)
    {
        var tags = RetryTypeTags(retryType);
        tags.Add("result", OutcomeNames[(int)outcome]);

        messages.Add(count, tags);
    }

    public RetryDurationScope BeginPreparation(RetryType retryType, CancellationToken cancellationToken = default) =>
        new(prepareDuration, timeProvider, RetryTypeTags(retryType), cancellationToken);

    public RetryDurationScope BeginStaging(RetryType retryType, CancellationToken cancellationToken = default) =>
        new(stageDuration, timeProvider, RetryTypeTags(retryType), cancellationToken);

    public RetryDurationScope BeginForwarding(RetryType retryType, bool recoveringFromPrematureShutdown, CancellationToken cancellationToken = default)
    {
        var tags = RetryTypeTags(retryType);
        tags.Add("mode", recoveringFromPrematureShutdown ? "timeout" : "counting");

        return new RetryDurationScope(forwardDuration, timeProvider, tags, cancellationToken);
    }

    public void ObserveOperationsInProgress(Func<IEnumerable<(RetryType RetryType, RetryState RetryState)>> operations) =>
        meter.CreateObservableGauge(OperationsInProgressInstrumentName, () => MeasureInProgress(operations()), description: "Retry operations currently in progress");

    public void ObservePendingBulkRequests(Func<int> queueDepth) =>
        meter.CreateObservableGauge(PendingBulkRequestsInstrumentName, () => (long)queueDepth(), description: "Bulk retry requests queued for preparation");

    static IEnumerable<Measurement<long>> MeasureInProgress(IEnumerable<(RetryType RetryType, RetryState RetryState)> operations)
    {
        foreach (var group in operations.Where(operation => operation.RetryState != RetryState.Completed).GroupBy(operation => operation))
        {
            var tags = RetryTypeTags(group.Key.RetryType);
            tags.Add("retry.state", StateNames[(int)group.Key.RetryState]);

            yield return new Measurement<long>(group.Count(), tags);
        }
    }

    static TagList RetryTypeTags(RetryType retryType) => new() { { "retry.type", RetryTypeNames[(int)retryType] } };

    // Indexed by the RetryType enum values.
    static readonly string[] RetryTypeNames = ["unknown", "single", "group", "batch", "endpoint", "all", "queue"];

    // Indexed by the RetryState enum values; Completed is never emitted.
    static readonly string[] StateNames = ["waiting", "preparing", "forwarding", "completed"];

    static readonly string[] OutcomeNames = ["staged", "forwarded", "skipped", "staging_retried", "abandoned"];

    readonly Meter meter;
    readonly TimeProvider timeProvider;
    readonly Histogram<double> operationDuration;
    readonly Histogram<double> prepareDuration;
    readonly Histogram<double> stageDuration;
    readonly Histogram<double> forwardDuration;
    readonly Counter<long> messages;

    const string MeterVersion = "0.1.0";
    const string InstrumentPrefix = "sc.retry";
}
