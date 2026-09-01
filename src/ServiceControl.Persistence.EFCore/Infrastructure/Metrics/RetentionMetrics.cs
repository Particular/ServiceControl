namespace ServiceControl.Persistence.EFCore.Infrastructure.Metrics;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using ServiceControl.Infrastructure;

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
        meter.CreateObservableGauge(ConsecutiveFailuresInstrumentName, ObserveConsecutiveFailures, description: "Consecutive retention sweep failures");
    }

    public RetentionCycleMetrics BeginCycle(RetentionEntity entity, CancellationToken cancellationToken = default) => new(this, entity, cancellationToken);

    public void RecordRowsDeleted(RetentionEntity entity, int rows) => rowsDeleted.Add(rows, EntityTags[(int)entity]);

    internal void RecordCycle(RetentionEntity entity, TimeSpan elapsed, CycleOutcome outcome)
    {
        var tags = EntityTags[(int)entity];
        tags.Add("result", ResultTag(outcome));

        cycleDuration.Record(elapsed.TotalSeconds, tags);

        // A pass cut short by shutdown neither proves the sweep healthy nor faulty, so it leaves
        // the gauge where it was.
        if (outcome == CycleOutcome.Success)
        {
            Interlocked.Exchange(ref consecutiveFailures[(int)entity], 0);
        }
        else if (outcome == CycleOutcome.Failed)
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

    static string ResultTag(CycleOutcome outcome) => outcome switch
    {
        CycleOutcome.Success => "success",
        CycleOutcome.Cancelled => "cancelled",
        CycleOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    readonly long[] consecutiveFailures = new long[EntityTags.Length];

    readonly Histogram<double> cycleDuration;
    readonly Counter<long> rowsDeleted;

    static readonly TagList[] EntityTags =
    [
        EntityTag("failed_messages"),
        EntityTag("event_log"),
        EntityTag("group_comments")
    ];

    const string MeterVersion = "0.1.0";
    const string InstrumentPrefix = "sc.retention";
}
