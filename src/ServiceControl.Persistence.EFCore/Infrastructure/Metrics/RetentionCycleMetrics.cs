namespace ServiceControl.Persistence.EFCore.Infrastructure.Metrics;

using System.Diagnostics;

/// <summary>
/// One pass of the retention sweep. A pass that finished counts as a success even if shutdown has
/// since been requested; one that shutdown cut short is recorded as cancelled rather than as a
/// failure.
/// </summary>
public sealed class RetentionCycleMetrics : IDisposable
{
    internal RetentionCycleMetrics(RetentionMetrics metrics, RetentionEntity entity, CancellationToken cancellationToken)
    {
        this.metrics = metrics;
        this.entity = entity;
        this.cancellationToken = cancellationToken;
    }

    // The pass is over once it completes, so the clock stops here rather than wherever the scope
    // happens to be disposed.
    public void Complete()
    {
        stopwatch.Stop();
        completed = true;
    }

    public void Dispose() => metrics.RecordCycle(entity, stopwatch.Elapsed, Outcome);

    CycleOutcome Outcome =>
        completed ? CycleOutcome.Success
        : cancellationToken.IsCancellationRequested ? CycleOutcome.Cancelled
        : CycleOutcome.Failed;

    bool completed;

    readonly RetentionMetrics metrics;
    readonly RetentionEntity entity;
    readonly CancellationToken cancellationToken;
    readonly Stopwatch stopwatch = Stopwatch.StartNew();
}
