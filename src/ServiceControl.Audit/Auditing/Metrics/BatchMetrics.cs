namespace ServiceControl.Audit.Auditing.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

public record BatchMetrics(int MaxBatchSize, Histogram<double> BatchDuration, Action<bool> IsSuccess) : IDisposable
{
    public void Dispose()
    {
        var isSuccess = actualBatchSize > 0;

        IsSuccess(isSuccess);

        string result;

        if (isSuccess)
        {
            result = actualBatchSize == MaxBatchSize ? "full" : "partial";
        }
        else
        {
            result = "failed";
        }

        BatchDuration.Record(sw.Elapsed.TotalSeconds, new TagList { { "result", result } });
    }

    public void Complete(int size) => actualBatchSize = size;

    int actualBatchSize = -1;
    readonly Stopwatch sw = Stopwatch.StartNew();
}