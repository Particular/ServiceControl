namespace ServiceControl.Infrastructure.Ingestion.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// How long the scope was open, in seconds, and nothing else.
/// </summary>
public sealed class DurationScope(Histogram<double> duration, Action<TimeSpan> recordDuration = null) : IDisposable
{
    public void Dispose()
    {
        duration.Record(stopwatch.Elapsed.TotalSeconds);
        recordDuration?.Invoke(stopwatch.Elapsed);
    }

    readonly Stopwatch stopwatch = Stopwatch.StartNew();
}