namespace ServiceControl.Audit;

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

record DurationRecorder(Histogram<double> Histogram) : IDisposable
{
    readonly Stopwatch sw = Stopwatch.StartNew();

    public void Dispose() => Histogram.Record(sw.ElapsedMilliseconds);
}