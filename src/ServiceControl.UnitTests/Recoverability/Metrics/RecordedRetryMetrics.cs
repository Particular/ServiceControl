namespace ServiceControl.UnitTests.Recoverability.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using ServiceControl.Recoverability.Retrying.Metrics;

    /// <summary>
    /// Collects everything the retry instruments record, for the meter belonging to one factory.
    /// Every fixture in the run shares the meter name, so the factory is what tells these
    /// instruments apart from the ones another test left behind.
    /// </summary>
    sealed class RecordedRetryMetrics : IDisposable
    {
        public RecordedRetryMetrics(IMeterFactory meterFactory)
        {
            listener = new MeterListener
            {
                InstrumentPublished = (instrument, activeListener) =>
                {
                    if (instrument.Meter.Name == RetryMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, meterFactory))
                    {
                        activeListener.EnableMeasurementEvents(instrument);
                    }
                }
            };

            listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => Add(instrument, measurement, tags));
            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => Add(instrument, measurement, tags));
            listener.Start();
        }

        public IReadOnlyList<Recorded> Of(string instrumentName)
        {
            lock (measurements)
            {
                return [.. measurements.Where(measurement => measurement.InstrumentName == instrumentName)];
            }
        }

        public IReadOnlyList<Recorded> Observe(string instrumentName)
        {
            lock (measurements)
            {
                measurements.RemoveAll(measurement => measurement.InstrumentName == instrumentName);
            }

            listener.RecordObservableInstruments();
            return Of(instrumentName);
        }

        public void Dispose() => listener.Dispose();

        void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            var recorded = new Recorded(instrument.Name, value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value));

            lock (measurements)
            {
                measurements.Add(recorded);
            }
        }

        readonly List<Recorded> measurements = [];
        readonly MeterListener listener;

        public sealed record Recorded(string InstrumentName, double Value, IReadOnlyDictionary<string, object> Tags);
    }
}
