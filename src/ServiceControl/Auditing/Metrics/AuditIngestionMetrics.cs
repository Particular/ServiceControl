namespace ServiceControl.Auditing.Metrics;

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using NServiceBus;
using NServiceBus.Transport;
using ServiceControl.EndpointPlugin.Messages.SagaState;
using ServiceControl.Infrastructure.Ingestion.Metrics;
using ServiceControl.Operations.Metrics;

/// <summary>
/// Mirrors <see cref="IngestionMetrics"/> for the audit queue. Same meter and the same primitives, so
/// the two ingestions report the same shapes and one exporter carries both; only the instrument
/// prefix and the tags differ, because what distinguishes an audit message is its kind rather than
/// whether it resolved a retry.
/// </summary>
public class AuditIngestionMetrics
{
    public static readonly string BatchDurationInstrumentName = $"{InstrumentPrefix}.batch_duration_seconds";
    public static readonly string MessageDurationInstrumentName = $"{InstrumentPrefix}.message_duration_seconds";
    public static readonly string StorageDurationInstrumentName = $"{InstrumentPrefix}.storage_duration_seconds";

    public AuditIngestionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(IngestionMetrics.MeterName, MeterVersion);

        batchDuration = meter.CreateHistogram<double>(BatchDurationInstrumentName, unit: "seconds", description: "Audit message batch processing duration in seconds");
        ingestionDuration = meter.CreateHistogram<double>(MessageDurationInstrumentName, unit: "seconds", description: "Audit message processing duration in seconds");
        storageDuration = meter.CreateHistogram<double>(StorageDurationInstrumentName, unit: "seconds", description: "Audit ingestion batch storage write duration in seconds");
        consecutiveBatchFailureGauge = meter.CreateObservableGauge($"{InstrumentPrefix}.consecutive_batch_failures_total", () => Volatile.Read(ref consecutiveBatchFailures), description: "Consecutive audit ingestion batch failures");
        failureCounter = meter.CreateCounter<long>($"{InstrumentPrefix}.failures_total", description: "Audit ingestion failure count");
    }

    public MessageMetrics BeginIngestion(MessageContext messageContext) => new(GetMessageTags(messageContext.Headers), ingestionDuration);

    public FailureMetrics BeginErrorHandling(ErrorContext errorContext) => new(GetMessageTags(errorContext.Headers), failureCounter);

    public BatchMetrics BeginBatch(int maxBatchSize) => new(maxBatchSize, batchDuration, RecordBatchOutcome);

    public DurationScope MeasureStorageWrite() => new(storageDuration);

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
            Volatile.Write(ref consecutiveBatchFailures, 0);
        }
        else
        {
            Interlocked.Increment(ref consecutiveBatchFailures);
        }
    }

    long consecutiveBatchFailures;

    readonly Histogram<double> batchDuration;
#pragma warning disable IDE0052
    readonly ObservableGauge<long> consecutiveBatchFailureGauge;
#pragma warning restore IDE0052
    readonly Histogram<double> ingestionDuration;
    readonly Histogram<double> storageDuration;
    readonly Counter<long> failureCounter;

    const string MeterVersion = "0.1.0";
    const string InstrumentPrefix = "sc.audit.ingestion";

    static readonly string SagaUpdateMessageType = typeof(SagaUpdatedMessage).FullName;
}
