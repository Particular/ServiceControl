namespace ServiceControl.Audit.Auditing.Metrics;

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using EndpointPlugin.Messages.SagaState;
using NServiceBus;
using NServiceBus.Transport;

public class IngestionMetrics
{
    public const string MeterName = "Particular.ServiceControl.Audit";

    public IngestionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, MeterVersion);

        var durationBucketsInSeconds = new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.01, 0.05, 0.1, 0.5, 1, 5] };

        batchDuration = meter.CreateHistogram(CreateInstrumentName("batch_duration_seconds"), unit: "seconds", "Message batch processing duration in seconds", advice: durationBucketsInSeconds);
        ingestionDuration = meter.CreateHistogram(CreateInstrumentName("message_duration_seconds"), unit: "seconds", description: "Audit message processing duration in seconds", advice: durationBucketsInSeconds);
        consecutiveBatchFailureGauge = meter.CreateGauge<long>(CreateInstrumentName("consecutive_batch_failure_total"), description: "Consecutive audit ingestion batch failure");
        failureCounter = meter.CreateCounter<long>(CreateInstrumentName("failures_total"), description: "Audit ingestion failure count");
    }

    public MessageMetrics BeginIngestion(MessageContext messageContext) => new(messageContext, ingestionDuration);

    public ErrorMetrics BeginErrorHandling(ErrorContext errorContext) => new(errorContext, failureCounter);

    public BatchMetrics BeginBatch(int maxBatchSize) => new(maxBatchSize, batchDuration, RecordBatchOutcome);

    public static TagList GetMessageTags(Dictionary<string, string> headers)
    {
        var tags = new TagList();

        if (headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageType))
        {
            tags.Add("message.category", messageType == SagaUpdateMessageType ? "saga-update" : "audit-message");
        }
        else
        {
            tags.Add("message.category", "control-message");
        }

        return tags;
    }

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

        consecutiveBatchFailureGauge.Record(consecutiveBatchFailures);
    }

    static string CreateInstrumentName(string instrumentName) => $"sc.audit.ingestion.{instrumentName.ToLower()}";

    long consecutiveBatchFailures;

    readonly Histogram<double> batchDuration;
    readonly Gauge<long> consecutiveBatchFailureGauge;
    readonly Histogram<double> ingestionDuration;
    readonly Counter<long> failureCounter;

    const string MeterVersion = "0.1.0";

    static readonly string SagaUpdateMessageType = typeof(SagaUpdatedMessage).FullName;
}