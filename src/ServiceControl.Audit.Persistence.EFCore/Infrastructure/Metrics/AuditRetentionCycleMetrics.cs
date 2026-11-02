namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure.Metrics;

using System.Diagnostics;

/// <summary>
/// Times one pass of the audit retention sweep and records it when disposed.
/// Call <see cref="Complete"/> when the pass finishes, and it counts as a success even if shutdown has since been requested.
/// Disposed without <see cref="Complete"/>, it counts as cancelled if shutdown was requested and as failed otherwise.
/// </summary>
public sealed class AuditRetentionCycleMetrics : IDisposable
{
    internal AuditRetentionCycleMetrics(AuditRetentionMetrics metrics, AuditRetentionEntity entity, CancellationToken cancellationToken)
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

    readonly AuditRetentionMetrics metrics;
    readonly AuditRetentionEntity entity;
    readonly CancellationToken cancellationToken;
    readonly Stopwatch stopwatch = Stopwatch.StartNew();
}
