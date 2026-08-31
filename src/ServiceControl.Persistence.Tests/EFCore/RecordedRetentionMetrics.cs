namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using ServiceControl.Persistence.EFCore.Infrastructure.Metrics;

/// <summary>
/// Collects everything the retention instruments record, for the meter belonging to one factory.
/// Every fixture in the run shares the meter name, so the factory is what tells these instruments
/// apart from the ones another test left behind.
/// </summary>
sealed class RecordedRetentionMetrics : IDisposable
{
    public RecordedRetentionMetrics(IMeterFactory meterFactory)
    {
        listener = new MeterListener
        {
            InstrumentPublished = (instrument, activeListener) =>
            {
                if (instrument.Meter.Name == RetentionMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, meterFactory))
                {
                    activeListener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) => Add(instrument, measurement, tags));
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) => Add(instrument, measurement, tags));
        listener.Start();
    }

    public IReadOnlyList<Recorded> Of(string instrumentName, RetentionEntity entity)
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

    public IReadOnlyList<Recorded> Cycles(RetentionEntity entity) => Of(RetentionMetrics.CycleDurationInstrumentName, entity);

    public double RowsDeleted(RetentionEntity entity) =>
        Of(RetentionMetrics.RowsDeletedInstrumentName, entity).Sum(measurement => measurement.Value);

    public double ConsecutiveFailures(RetentionEntity entity)
    {
        listener.RecordObservableInstruments();

        return Of(RetentionMetrics.ConsecutiveFailuresInstrumentName, entity)[^1].Value;
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

    static string EntityTag(RetentionEntity entity) => entity switch
    {
        RetentionEntity.FailedMessages => "failed_messages",
        RetentionEntity.EventLog => "event_log",
        RetentionEntity.GroupComments => "group_comments",
        _ => throw new ArgumentOutOfRangeException(nameof(entity))
    };

    readonly MeterListener listener;
    readonly List<Recorded> measurements = [];

    public sealed record Recorded(string InstrumentName, double Value, Dictionary<string, object> Tags)
    {
        public object Result => Tags["result"];
    }
}
