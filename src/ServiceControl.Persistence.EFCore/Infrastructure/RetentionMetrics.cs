namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using ServiceControl.Infrastructure;

public enum RetentionEntity
{
    FailedMessages,
    EventLog,
    GroupComments
}

public class RetentionMetrics
{
    public const string MeterName = ServiceControlMeters.Error;

    public static readonly string CycleDurationInstrumentName = $"{InstrumentPrefix}.cycle_duration_seconds";
    public static readonly string RowsDeletedInstrumentName = $"{InstrumentPrefix}.rows_deleted_total";
    public static readonly string ConsecutiveFailuresInstrumentName = $"{InstrumentPrefix}.consecutive_failures_total";

    public RetentionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, MeterVersion);

        cycleDuration = meter.CreateHistogram(
            CycleDurationInstrumentName,
            unit: "seconds",
            description: "Retention sweep pass duration in seconds",
            tags: null,
            // A sweep pass is sub-second when it is keeping up and minutes long when it is working
            // through a backlog, so the default boundaries resolve neither end.
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.1, 0.5, 1, 5, 15, 60, 300, 900] });

        rowsDeleted = meter.CreateCounter<long>(RowsDeletedInstrumentName, description: "Rows deleted by the retention sweep");
        consecutiveFailureGauge = meter.CreateObservableGauge(ConsecutiveFailuresInstrumentName, ObserveConsecutiveFailures, description: "Consecutive retention sweep failures");
    }

    public RetentionCycleMetrics BeginCycle(RetentionEntity entity, CancellationToken cancellationToken = default) => new(this, entity, cancellationToken);

    public void RecordRowsDeleted(RetentionEntity entity, int rows) => rowsDeleted.Add(rows, EntityTags[(int)entity]);

    internal void RecordCycle(RetentionEntity entity, TimeSpan elapsed, bool success)
    {
        var tags = EntityTags[(int)entity];
        tags.Add("result", success ? "success" : "failed");

        cycleDuration.Record(elapsed.TotalSeconds, tags);

        if (success)
        {
            Interlocked.Exchange(ref consecutiveFailures[(int)entity], 0);
        }
        else
        {
            Interlocked.Increment(ref consecutiveFailures[(int)entity]);
        }
    }

    IEnumerable<Measurement<long>> ObserveConsecutiveFailures()
    {
        for (var entity = 0; entity < consecutiveFailures.Length; entity++)
        {
            yield return new Measurement<long>(Volatile.Read(ref consecutiveFailures[entity]), EntityTags[entity]);
        }
    }

    static TagList EntityTag(string entity) => new() { { "retention.entity", entity } };

    readonly long[] consecutiveFailures = new long[EntityTags.Length];

    readonly Histogram<double> cycleDuration;
    readonly Counter<long> rowsDeleted;
#pragma warning disable IDE0052
    readonly ObservableGauge<long> consecutiveFailureGauge;
#pragma warning restore IDE0052

    static readonly TagList[] EntityTags =
    [
        EntityTag("failed_messages"),
        EntityTag("event_log"),
        EntityTag("group_comments")
    ];

    const string MeterVersion = "0.1.0";
    const string InstrumentPrefix = "sc.retention";
}

/// <summary>
/// One pass of the retention sweep. A pass interrupted by shutdown is not a measurement of
/// anything, so a cancelled cycle records neither a duration nor a failure.
/// </summary>
public sealed class RetentionCycleMetrics : IDisposable
{
    internal RetentionCycleMetrics(RetentionMetrics metrics, RetentionEntity entity, CancellationToken cancellationToken)
    {
        this.metrics = metrics;
        this.entity = entity;
        this.cancellationToken = cancellationToken;
    }

    public void Complete() => completed = true;

    public void Dispose()
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        metrics.RecordCycle(entity, stopwatch.Elapsed, completed);
    }

    bool completed;

    readonly RetentionMetrics metrics;
    readonly RetentionEntity entity;
    readonly CancellationToken cancellationToken;
    readonly Stopwatch stopwatch = Stopwatch.StartNew();
}
