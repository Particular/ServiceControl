namespace ServiceControl.Audit.Auditing.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

public record BatchMetrics(int MaxBatchSize, Histogram<double> BatchDuration, Action<bool> IsSuccess) : IDisposable
{
    public void Dispose()
    {
        var tags = new TagList();

        string result;

        if (actualBatchSize <= 0)
        {
            result = "failed";
            IsSuccess(false);
        }
        else
        {
            result = actualBatchSize == MaxBatchSize ? "full" : "partial";

            IsSuccess(true);
        }

        tags.Add("result", result);
        BatchDuration.Record(sw.ElapsedMilliseconds, tags);
    }

    public void Complete(int size) => actualBatchSize = size;

    int actualBatchSize = -1;
    readonly Stopwatch sw = Stopwatch.StartNew();
}