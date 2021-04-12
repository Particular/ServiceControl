namespace ServiceControl.Infrastructure.Metrics
{
    using System.Collections.Generic;
    using System.Linq;

    public class Metrics
    {
        readonly Dictionary<string, Counter> counters = new Dictionary<string, Counter>();
        readonly Dictionary<string, Meter> meters = new Dictionary<string, Meter>();

        public Counter GetCounter(string name)
        {
            if (!counters.TryGetValue(name, out var meter))
            {
                meter = new Counter(name);
                counters[name] = meter;
            }

            return meter;
        }

        public Meter GetMeter(string name, float scale = 1)
        {
            if (!meters.TryGetValue(name, out var meter))
            {
                meter = new Meter(name, scale);
                meters[name] = meter;
            }

            return meter;
        }

        public IReadOnlyCollection<MeterValues> GetMeterValues()
        {
            var counterValues = counters.Values.Select(x => x.GetValues());
            var meterValues = meters.Values.Select(x => x.GetValues());
            return counterValues.Concat(meterValues).ToArray();
        }
    }
}