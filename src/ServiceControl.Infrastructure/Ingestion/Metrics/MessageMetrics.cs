namespace ServiceControl.Infrastructure.Ingestion.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// One message, from being received to its batch being written. Leaving the scope without saying
/// otherwise records it as failed.
/// </summary>
public sealed class MessageMetrics(TagList messageTags, Histogram<double> duration) : IDisposable
{
    public void Skipped() => result = "skipped";

    public void Success() => result = "success";

    public void Dispose()
    {
        var tags = messageTags;
        tags.Add("result", result);

        duration.Record(stopwatch.Elapsed.TotalSeconds, tags);
    }

    string result = "failed";
    readonly Stopwatch stopwatch = Stopwatch.StartNew();
}