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

    public static readonly string BatchDurationInstrumentName = $"{InstrumentPrefix}.batch_duration_seconds";
    public static readonly string MessageDurationInstrumentName = $"{InstrumentPrefix}.message_duration_seconds";

    public IngestionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, MeterVersion);

        batchDuration = meter.CreateHistogram<double>(BatchDurationInstrumentName, unit: "seconds", "Message batch processing duration in seconds");
        ingestionDuration = meter.CreateHistogram<double>(MessageDurationInstrumentName, unit: "seconds", description: "Audit message processing duration in seconds");
        consecutiveBatchFailureGauge = meter.CreateGauge<long>($"{InstrumentPrefix}.consecutive_batch_failure_total", description: "Consecutive audit ingestion batch failure");
        failureCounter = meter.CreateCounter<long>($"{InstrumentPrefix}.failures_total", description: "Audit ingestion failure count");
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

    long consecutiveBatchFailures;

    readonly Histogram<double> batchDuration;
    readonly Gauge<long> consecutiveBatchFailureGauge;
    readonly Histogram<double> ingestionDuration;
    readonly Counter<long> failureCounter;

    const string MeterVersion = "0.1.0";
    const string InstrumentPrefix = "sc.audit.ingestion";

    static readonly string SagaUpdateMessageType = typeof(SagaUpdatedMessage).FullName;
}