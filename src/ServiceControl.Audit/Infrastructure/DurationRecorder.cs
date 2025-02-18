namespace ServiceControl.Infrastructure;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

record DurationRecorder(Histogram<double> Histogram, TagList Tags = default) : IDisposable
{
    readonly Stopwatch sw = Stopwatch.StartNew();

    public void Dispose() => Histogram.Record(sw.ElapsedMilliseconds, Tags);
}