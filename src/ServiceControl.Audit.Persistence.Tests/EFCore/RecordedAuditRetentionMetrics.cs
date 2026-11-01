namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure.Metrics;

    /// <summary>
    /// Collects everything the audit retention instruments record, for the meter belonging to one factory.
    /// Every fixture in the run shares the meter name, so the factory is what tells these instruments
    /// apart from the ones another test left behind.
    /// </summary>
    sealed class RecordedAuditRetentionMetrics : IDisposable
    {
        public RecordedAuditRetentionMetrics(IMeterFactory meterFactory)
        {
            listener = new MeterListener
            {
                InstrumentPublished = (instrument, activeListener) =>
                {
                    if (instrument.Meter.Name == AuditRetentionMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, meterFactory))
                    {
                        activeListener.EnableMeasurementEvents(instrument);
                    }
                }
            };

            listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => Add(instrument, measurement, tags));
            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => Add(instrument, measurement, tags));
            listener.Start();
        }

        public IReadOnlyList<Recorded> Of(string instrumentName, AuditRetentionEntity entity)
        {
            lock (measurements)
            {
                return
                [
                    .. measurements.Where(measurement =>
                        measurement.InstrumentName == instrumentName &&
                        Equals(measurement.Tags["retention.entity"], EntityTag(entity)))
                ];
            }
        }

        public IReadOnlyList<Recorded> Cycles(AuditRetentionEntity entity) => Of(AuditRetentionMetrics.CycleDurationInstrumentName, entity);

        public IReadOnlyList<string> CycleResults(AuditRetentionEntity entity) => [.. Cycles(entity).Select(cycle => cycle.Result)];

        public double ConsecutiveFailures(AuditRetentionEntity entity)
        {
            listener.RecordObservableInstruments();

            return Of(AuditRetentionMetrics.ConsecutiveFailuresInstrumentName, entity)[^1].Value;
        }

        public bool RecordedAnything()
        {
            lock (measurements)
            {
                return measurements.Any(measurement => measurement.InstrumentName != AuditRetentionMetrics.ConsecutiveFailuresInstrumentName);
            }
        }

        public void Dispose() => listener.Dispose();

        void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            var copied = new Dictionary<string, object>();

            foreach (var tag in tags)
            {
                copied[tag.Key] = tag.Value;
            }

            lock (measurements)
            {
                measurements.Add(new Recorded(instrument.Name, value, copied));
            }
        }

        static string EntityTag(AuditRetentionEntity entity) => entity switch
        {
            AuditRetentionEntity.AuditMessages => "audit_messages",
            AuditRetentionEntity.SagaSnapshots => "saga_snapshots",
            _ => throw new ArgumentOutOfRangeException(nameof(entity))
        };

        readonly MeterListener listener;
        readonly List<Recorded> measurements = [];

        public sealed record Recorded(string InstrumentName, double Value, IReadOnlyDictionary<string, object> Tags)
        {
            public string Result => (string)Tags["result"];
        }
    }
}
