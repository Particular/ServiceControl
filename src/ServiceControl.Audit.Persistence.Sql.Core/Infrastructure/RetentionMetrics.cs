namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using System.Diagnostics;
using System.Diagnostics.Metrics;

public class RetentionMetrics
{
    public const string MeterName = "Particular.ServiceControl.Audit";

    public static readonly string CleanupDurationInstrumentName = $"{InstrumentPrefix}.cleanup_duration";
    public static readonly string BatchDurationInstrumentName = $"{InstrumentPrefix}.batch_duration";
    public static readonly string MessagesDeletedInstrumentName = $"{InstrumentPrefix}.messages_deleted_total";
    public static readonly string SagaSnapshotsDeletedInstrumentName = $"{InstrumentPrefix}.saga_snapshots_deleted_total";

    public RetentionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, MeterVersion);

        cleanupDuration = meter.CreateHistogram<double>(CleanupDurationInstrumentName, unit: "s", description: "Retention cleanup cycle duration");
        batchDuration = meter.CreateHistogram<double>(BatchDurationInstrumentName, unit: "s", description: "Retention cleanup batch duration");
        messagesDeleted = meter.CreateCounter<long>(MessagesDeletedInstrumentName, description: "Total audit messages deleted by retention cleanup");
        sagaSnapshotsDeleted = meter.CreateCounter<long>(SagaSnapshotsDeletedInstrumentName, description: "Total saga snapshots deleted by retention cleanup");
        consecutiveFailureGauge = meter.CreateObservableGauge($"{InstrumentPrefix}.consecutive_failures_total", () => consecutiveFailures, description: "Consecutive retention cleanup failures");
        lockSkippedCounter = meter.CreateCounter<long>($"{InstrumentPrefix}.lock_skipped_total", description: "Number of times cleanup was skipped due to another instance holding the lock");
    }

    public CleanupCycleMetrics BeginCleanupCycle() => new(cleanupDuration, RecordCycleOutcome);

    public BatchOperationMetrics BeginBatch(string entityType) => new(batchDuration, messagesDeleted, sagaSnapshotsDeleted, entityType);

    public void RecordLockSkipped() => lockSkippedCounter.Add(1);

    void RecordCycleOutcome(bool success)
    {
        if (success)
        {
            consecutiveFailures = 0;
        }
        else
        {
            consecutiveFailures++;
        }
    }

    long consecutiveFailures;

    readonly Histogram<double> cleanupDuration;
    readonly Histogram<double> batchDuration;
    readonly Counter<long> messagesDeleted;
    readonly Counter<long> sagaSnapshotsDeleted;
#pragma warning disable IDE0052
    readonly ObservableGauge<long> consecutiveFailureGauge;
#pragma warning restore IDE0052
    readonly Counter<long> lockSkippedCounter;

    const string MeterVersion = "0.1.0";
    const string InstrumentPrefix = "sc.audit.retention";
}

public class CleanupCycleMetrics : IDisposable
{
    readonly Histogram<double> cleanupDuration;
    readonly Action<bool> recordOutcome;
    readonly Stopwatch stopwatch = Stopwatch.StartNew();

    bool completed;

    internal CleanupCycleMetrics(Histogram<double> cleanupDuration, Action<bool> recordOutcome)
    {
        this.cleanupDuration = cleanupDuration;
        this.recordOutcome = recordOutcome;
    }

    public void Complete() => completed = true;

    public void Dispose()
    {
        var result = completed ? "success" : "failed";
        var tags = new TagList { { "result", result } };

        cleanupDuration.Record(stopwatch.Elapsed.TotalSeconds, tags);
        recordOutcome(completed);
    }
}

public class BatchOperationMetrics : IDisposable
{
    readonly Histogram<double> batchDuration;
    readonly Counter<long> messagesDeleted;
    readonly Counter<long> sagaSnapshotsDeleted;
    readonly string entityType;
    readonly Stopwatch stopwatch = Stopwatch.StartNew();

    int deletedCount;

    internal BatchOperationMetrics(
        Histogram<double> batchDuration,
        Counter<long> messagesDeleted,
        Counter<long> sagaSnapshotsDeleted,
        string entityType)
    {
        this.batchDuration = batchDuration;
        this.messagesDeleted = messagesDeleted;
        this.sagaSnapshotsDeleted = sagaSnapshotsDeleted;
        this.entityType = entityType;
    }

    public void RecordDeleted(int count) => deletedCount = count;

    public void Dispose()
    {
        var tags = new TagList { { "entity_type", entityType } };

        batchDuration.Record(stopwatch.Elapsed.TotalSeconds, tags);

        if (entityType == EntityTypes.Message)
        {
            messagesDeleted.Add(deletedCount);
        }
        else if (entityType == EntityTypes.SagaSnapshot)
        {
            sagaSnapshotsDeleted.Add(deletedCount);
        }
    }
}

public static class EntityTypes
{
    public const string Message = "message";
    public const string SagaSnapshot = "saga_snapshot";
}
