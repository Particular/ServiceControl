namespace ServiceControl.Infrastructure.Ingestion.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// One batch write. Leaving the scope without calling <see cref="Complete" /> is what records the
/// batch as failed, so nothing has to be told about the exception that ended it.
/// </summary>
public sealed class BatchMetrics(int maxBatchSize, Histogram<double> batchDuration, Action<bool> recordOutcome) : IDisposable
{
    public void Complete(int batchSize) => completedSize = batchSize;

    public void Dispose()
    {
        var succeeded = completedSize > 0;

        recordOutcome(succeeded);

        var result = succeeded
            ? completedSize == maxBatchSize ? "full" : "partial"
            : "failed";

        batchDuration.Record(stopwatch.Elapsed.TotalSeconds, new TagList { { "result", result } });
    }

    int completedSize = -1;
    readonly Stopwatch stopwatch = Stopwatch.StartNew();
}