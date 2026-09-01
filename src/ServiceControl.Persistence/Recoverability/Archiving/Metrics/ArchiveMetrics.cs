namespace ServiceControl.Recoverability.Archiving.Metrics;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using ServiceControl.Infrastructure;

/// <summary>
/// The sc.archive.* instruments.
/// </summary>
public class ArchiveMetrics
{
    public const string MeterName = ServiceControlMeters.Error;

    public static readonly string OperationDurationInstrumentName = $"{InstrumentPrefix}.operation_duration_seconds";
    public static readonly string BatchDurationInstrumentName = $"{InstrumentPrefix}.batch_duration_seconds";
    public static readonly string MessagesInstrumentName = $"{InstrumentPrefix}.messages_total";
    public static readonly string OperationsInProgressInstrumentName = $"{InstrumentPrefix}.operations_in_progress";

    public ArchiveMetrics(IMeterFactory meterFactory, TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
        var meter = meterFactory.Create(MeterName, MeterVersion);

        operationDuration = meter.CreateHistogram(
            OperationDurationInstrumentName,
            unit: "seconds",
            description: "Group archive or unarchive operation duration in seconds",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.5, 1, 5, 15, 60, 300, 900, 3600] });

        batchDuration = meter.CreateHistogram(
            BatchDurationInstrumentName,
            unit: "seconds",
            description: "Archive batch duration in seconds",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.05, 0.1, 0.5, 1, 5, 15, 60] });

        messages = meter.CreateCounter<long>(MessagesInstrumentName, description: "Messages archived and unarchived");
        meter.CreateObservableGauge(OperationsInProgressInstrumentName, ObserveInProgress, description: "Archive operations currently in progress");
    }

    public ArchiveOperationMetrics CreateOperation(ArchiveOperationKind kind) => new(this, kind);

    internal long GetTimestamp() => timeProvider.GetTimestamp();

    internal void RecordBatch(ArchiveOperationKind kind, long sinceTimestamp, int messagesInBatch)
    {
        batchDuration.Record(timeProvider.GetElapsedTime(sinceTimestamp).TotalSeconds, KindTags[(int)kind]);
        messages.Add(messagesInBatch, KindTags[(int)kind]);
    }

    internal void RecordOperationCompleted(ArchiveOperationKind kind, long startTimestamp) =>
        operationDuration.Record(timeProvider.GetElapsedTime(startTimestamp).TotalSeconds, KindTags[(int)kind]);

    internal void RecordStateTransition(ArchiveOperationKind kind, ArchiveState? from, ArchiveState to)
    {
        if (from is { } previous && TryStateBucket(previous, out var fromBucket))
        {
            Interlocked.Decrement(ref inProgress[Index(kind, fromBucket)]);
        }

        if (TryStateBucket(to, out var toBucket))
        {
            Interlocked.Increment(ref inProgress[Index(kind, toBucket)]);
        }
    }

    IEnumerable<Measurement<long>> ObserveInProgress()
    {
        for (var kind = 0; kind < KindTags.Length; kind++)
        {
            for (var state = 0; state < StateNames.Length; state++)
            {
                var tags = KindTags[kind];
                tags.Add("archive.state", StateNames[state]);

                yield return new Measurement<long>(Volatile.Read(ref inProgress[Index((ArchiveOperationKind)kind, state)]), tags);
            }
        }
    }

    static bool TryStateBucket(ArchiveState state, out int bucket)
    {
        bucket = (int)state;
        return state != ArchiveState.ArchiveCompleted;
    }

    static int Index(ArchiveOperationKind kind, int stateBucket) => ((int)kind * StateNames.Length) + stateBucket;

    // Indexed by the ArchiveState enum values; Completed is never in progress.
    static readonly string[] StateNames = ["started", "progressing", "finalizing"];

    static readonly TagList[] KindTags =
    [
        new() { { "archive.operation", "archive" } },
        new() { { "archive.operation", "unarchive" } }
    ];

    readonly long[] inProgress = new long[KindTags.Length * StateNames.Length];

    readonly TimeProvider timeProvider;
    readonly Histogram<double> operationDuration;
    readonly Histogram<double> batchDuration;
    readonly Counter<long> messages;

    const string MeterVersion = "0.1.0";
    const string InstrumentPrefix = "sc.archive";
}
