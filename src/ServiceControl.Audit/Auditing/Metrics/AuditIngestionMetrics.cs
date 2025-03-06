namespace ServiceControl.Audit.Auditing.Metrics;

using System.Diagnostics.Metrics;
using NServiceBus.Transport;

public class AuditIngestionMetrics
{
    public AuditIngestionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, MeterVersion);

        batchDuration = meter.CreateHistogram<double>(CreateInstrumentName("batch_duration"), unit: "ms", "Average audit message batch processing duration");
        consecutiveBatchFailureGauge = meter.CreateObservableGauge(CreateInstrumentName("consecutive_batch_failures"), () => consecutiveBatchFailures, unit: "count", description: "Consecutive audit ingestion batch failure");
        ingestionDuration = meter.CreateHistogram<double>(CreateInstrumentName("duration"), unit: "ms", description: "Average incoming audit message processing duration");
    }

    public MessageIngestionMetrics BeginIngestion(MessageContext messageContext) => new(messageContext, ingestionDuration);

    public BatchMetrics BeginBatch(int maxBatchSize) => new(maxBatchSize, batchDuration, RecordBatchOutcome);

    void RecordBatchOutcome(bool success)
    {
        if (success)
        {
            consecutiveBatchFailures = 0;
        }
        else
        {
            consecutiveBatchFailures++;
        }
    }

    static string CreateInstrumentName(string instrumentName) => $"sc.audit.ingestion.{instrumentName}".ToLower();

    long consecutiveBatchFailures;

    readonly Histogram<double> batchDuration;
#pragma warning disable IDE0052
    readonly ObservableGauge<long> consecutiveBatchFailureGauge;
#pragma warning restore IDE0052
    readonly Histogram<double> ingestionDuration;

    const string MeterName = "Particular.ServiceControl.Audit";
    const string MeterVersion = "0.1.0";
}