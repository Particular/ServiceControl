namespace ServiceControl.Audit.Auditing.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

public record BatchMetrics(int MaxBatchSize, Histogram<double> BatchDuration, Action<bool> IsSuccess) : IDisposable
{
    public void Dispose()
    {
        var tags = new TagList();

        var isSuccess = actualBatchSize > 0;

        IsSuccess(isSuccess);

        if (isSuccess)
        {
            var result = actualBatchSize == MaxBatchSize ? "full" : "partial";
            tags.Add("result", result);
        }

        BatchDuration.Record(sw.Elapsed.TotalSeconds, tags);
    }

    public void Complete(int size) => actualBatchSize = size;

    int actualBatchSize = -1;
    readonly Stopwatch sw = Stopwatch.StartNew();
}