namespace ServiceControl.Audit.Auditing.Metrics;

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using EndpointPlugin.Messages.SagaState;
using NServiceBus;
using NServiceBus.Transport;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Ingestion.Metrics;

public class IngestionMetrics
{
    public const string MeterName = ServiceControlMeters.Audit;

    public static readonly string BatchDurationInstrumentName = $"{InstrumentPrefix}.batch_duration_seconds";
    public static readonly string MessageDurationInstrumentName = $"{InstrumentPrefix}.message_duration_seconds";

    public IngestionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, MeterVersion);

        batchDuration = meter.CreateHistogram<double>(BatchDurationInstrumentName, unit: "seconds", "Message batch processing duration in seconds");
        ingestionDuration = meter.CreateHistogram<double>(MessageDurationInstrumentName, unit: "seconds", description: "Audit message processing duration in seconds");
        meter.CreateObservableGauge($"{InstrumentPrefix}.consecutive_batch_failures_total", () => Volatile.Read(ref consecutiveBatchFailures), description: "Consecutive audit ingestion batch failures");
        failureCounter = meter.CreateCounter<long>($"{InstrumentPrefix}.failures_total", description: "Audit ingestion failure count");
    }

    public MessageMetrics BeginIngestion(MessageContext messageContext) => new(GetMessageTags(messageContext.Headers), ingestionDuration);

    public FailureMetrics BeginErrorHandling(ErrorContext errorContext) => new(GetMessageTags(errorContext.Headers), failureCounter);

    public BatchMetrics BeginBatch(int maxBatchSize) => new(maxBatchSize, batchDuration, RecordBatchOutcome);

    static TagList GetMessageTags(Dictionary<string, string> headers)
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
            Interlocked.Exchange(ref consecutiveBatchFailures, 0);
        }
        else
        {
            Interlocked.Increment(ref consecutiveBatchFailures);
        }
    }

    long consecutiveBatchFailures;

    readonly Histogram<double> batchDuration;
    readonly Histogram<double> ingestionDuration;
    readonly Counter<long> failureCounter;

    const string MeterVersion = "0.1.0";
    const string InstrumentPrefix = "sc.audit.ingestion";

    static readonly string SagaUpdateMessageType = typeof(SagaUpdatedMessage).FullName;
}