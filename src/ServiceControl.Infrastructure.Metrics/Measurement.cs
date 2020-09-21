using System;
using System.Diagnostics;

namespace ServiceControl.Infrastructure
{
    public readonly struct Measurement : IDisposable
    {
        readonly Meter meter;
        readonly long startTimestamp;

        public Measurement(Meter meter)
        {
            this.meter = meter;
            startTimestamp = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            var endTimestamp = Stopwatch.GetTimestamp();
            var duration = endTimestamp - startTimestamp;
            meter.Mark(duration);
        }
    }
}