namespace ServiceControl.Recoverability.Archiving.Metrics;

public sealed class ArchiveOperationMetrics
{
    internal ArchiveOperationMetrics(ArchiveMetrics metrics, ArchiveOperationKind kind)
    {
        this.metrics = metrics;
        this.kind = kind;
    }

    public void Started()
    {
        Transition(ArchiveState.ArchiveStarted);
        operationStartTimestamp = metrics.GetTimestamp();
        lastBatchTimestamp = operationStartTimestamp;
    }

    public void BatchCompleted(int messagesInBatch)
    {
        Transition(ArchiveState.ArchiveProgressing);
        metrics.RecordBatch(kind, lastBatchTimestamp, messagesInBatch);
        lastBatchTimestamp = metrics.GetTimestamp();
    }

    public void Finalizing() => Transition(ArchiveState.ArchiveFinalizing);

    public void Completed()
    {
        var started = trackedState is not null and not ArchiveState.ArchiveCompleted;
        Transition(ArchiveState.ArchiveCompleted);

        if (started)
        {
            metrics.RecordOperationCompleted(kind, operationStartTimestamp);
        }
    }

    void Transition(ArchiveState to)
    {
        metrics.RecordStateTransition(kind, trackedState, to);
        trackedState = to;
    }

    ArchiveState? trackedState;
    long operationStartTimestamp;
    long lastBatchTimestamp;

    readonly ArchiveMetrics metrics;
    readonly ArchiveOperationKind kind;
}
