namespace ServiceControl.Infrastructure.Metrics
{
    using System;
    using System.Diagnostics;

    public readonly struct Measurement : IDisposable
    {
        readonly Meter meter;
        readonly bool enabled;
        readonly long startTimestamp;

        public Measurement(Meter meter, bool enabled)
        {
            this.meter = meter;
            this.enabled = enabled;
            if (enabled)
            {
                startTimestamp = Stopwatch.GetTimestamp();
            }
            else
            {
                startTimestamp = 0;
            }
        }

        public void Dispose()
        {
            if (!enabled)
            {
                return;
            }
            var endTimestamp = Stopwatch.GetTimestamp();
            var duration = endTimestamp - startTimestamp;
            meter.Mark(duration);
        }
    }
}