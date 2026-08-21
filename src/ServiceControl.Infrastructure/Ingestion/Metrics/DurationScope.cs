namespace ServiceControl.Infrastructure.Ingestion.Metrics;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// How long the scope was open, in seconds, and nothing else.
/// </summary>
public sealed class DurationScope(Histogram<double> duration) : IDisposable
{
    public void Dispose() => duration.Record(stopwatch.Elapsed.TotalSeconds);

    readonly Stopwatch stopwatch = Stopwatch.StartNew();
}