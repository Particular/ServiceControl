namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using ServiceControl.Recoverability.Archiving.Metrics;

/// <summary>
/// Collects everything the archive instruments record, for the meter belonging to one factory.
/// Every fixture in the run shares the meter name, so the factory is what tells these instruments
/// apart from the ones another test left behind.
/// </summary>
sealed class RecordedArchiveMetrics : IDisposable
{
    public RecordedArchiveMetrics(IMeterFactory meterFactory)
    {
        listener = new MeterListener
        {
            InstrumentPublished = (instrument, activeListener) =>
            {
                if (instrument.Meter.Name == ArchiveMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, meterFactory))
                {
                    activeListener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => Add(instrument, measurement, tags));
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => Add(instrument, measurement, tags));
        listener.Start();
    }

    public IReadOnlyList<Recorded> Of(string instrumentName, ArchiveOperationKind kind)
    {
        lock (measurements)
        {
            return
            [
                .. measurements.Where(measurement =>
                    measurement.InstrumentName == instrumentName &&
                    Equals(measurement.Tags["archive.operation"], KindName(kind)))
            ];
        }
    }

    public IReadOnlyList<Recorded> BatchDurations(ArchiveOperationKind kind) => Of(ArchiveMetrics.BatchDurationInstrumentName, kind);

    public IReadOnlyList<Recorded> OperationDurations(ArchiveOperationKind kind) => Of(ArchiveMetrics.OperationDurationInstrumentName, kind);

    public double Messages(ArchiveOperationKind kind) =>
        Of(ArchiveMetrics.MessagesInstrumentName, kind).Sum(measurement => measurement.Value);

    public double InProgress(ArchiveOperationKind kind, string state)
    {
        lock (measurements)
        {
            measurements.RemoveAll(measurement => measurement.InstrumentName == ArchiveMetrics.OperationsInProgressInstrumentName);
        }

        listener.RecordObservableInstruments();

        return Of(ArchiveMetrics.OperationsInProgressInstrumentName, kind)
            .Single(measurement => Equals(measurement.Tags["archive.state"], state)).Value;
    }

    public void Dispose() => listener.Dispose();

    static string KindName(ArchiveOperationKind kind) => kind == ArchiveOperationKind.Archive ? "archive" : "unarchive";

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
