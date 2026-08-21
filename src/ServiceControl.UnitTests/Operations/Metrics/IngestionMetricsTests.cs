namespace ServiceControl.UnitTests.Operations.Metrics;

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Operations.Metrics;

/// <summary>
/// Instrument names are what dashboards and alerts are built on, so they are a published contract
/// and not an implementation detail.
/// </summary>
[TestFixture]
class IngestionMetricsTests
{
    [SetUp]
    public void CreateMeterFactory() => provider = new ServiceCollection().AddMetrics().BuildServiceProvider();

    [TearDown]
    public void DisposeMeterFactory() => provider.Dispose();

    [Test]
    public void The_meter_publishes_the_instruments_it_is_named_for()
    {
        var published = new List<string>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (BelongsToThisTest(instrument))
                {
                    published.Add(instrument.Name);
                }
            }
        };

        listener.Start();

        _ = new IngestionMetrics(MeterFactory);

        Assert.That(published.Order(), Is.EqualTo(new[]
        {
            "sc.error.ingestion.batch_duration_seconds",
            "sc.error.ingestion.consecutive_batch_failures_total",
            "sc.error.ingestion.failures_total",
            "sc.error.ingestion.message_duration_seconds",
            "sc.error.ingestion.storage_duration_seconds"
        }));
    }

    [Test]
    public void Concurrent_batch_failures_are_all_counted()
    {
        var metrics = new IngestionMetrics(MeterFactory);

        const int failedBatches = 1000;

        Parallel.For(0, failedBatches, _ =>
        {
            using var batch = metrics.BeginBatch(maxBatchSize: 1);
        });

        Assert.That(ReadConsecutiveBatchFailures(), Is.EqualTo(failedBatches));
    }

    long ReadConsecutiveBatchFailures()
    {
        long value = -1;

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, activeListener) =>
            {
                if (BelongsToThisTest(instrument) && instrument.Name == "sc.error.ingestion.consecutive_batch_failures_total")
                {
                    activeListener.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => value = measurement);
        listener.Start();
        listener.RecordObservableInstruments();

        return value;
    }

    // Every fixture in the run shares the meter name, so the factory is what tells the instruments
    // created here apart from the ones another test left behind.
    bool BelongsToThisTest(Instrument instrument) =>
        instrument.Meter.Name == IngestionMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, MeterFactory);

    IMeterFactory MeterFactory => provider.GetRequiredService<IMeterFactory>();

    ServiceProvider provider;
}
