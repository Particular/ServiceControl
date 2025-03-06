namespace ServiceControl.Audit.Auditing.Metrics;

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using EndpointPlugin.Messages.SagaState;
using NServiceBus;
using NServiceBus.Transport;

public class IngestionMetrics
{
    public IngestionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, MeterVersion);

        batchDuration = meter.CreateHistogram<double>(CreateInstrumentName("batch_duration"), unit: "ms", "Average audit message batch processing duration");
        consecutiveBatchFailureGauge = meter.CreateObservableGauge(CreateInstrumentName("consecutive_batch_failures"), () => consecutiveBatchFailures, description: "Consecutive audit ingestion batch failure");
        ingestionDuration = meter.CreateHistogram<double>(CreateInstrumentName("duration"), unit: "ms", description: "Average incoming audit message processing duration");
        failureCounter = meter.CreateCounter<long>(CreateInstrumentName("failure_count"), description: "Audit ingestion failure count");
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
    }

    static string CreateInstrumentName(string instrumentName) => $"sc.audit.ingestion.{instrumentName}".ToLower();

    long consecutiveBatchFailures;

    readonly Histogram<double> batchDuration;
#pragma warning disable IDE0052
    readonly ObservableGauge<long> consecutiveBatchFailureGauge;
#pragma warning restore IDE0052
    readonly Histogram<double> ingestionDuration;
    readonly Counter<long> failureCounter;

    const string MeterName = "Particular.ServiceControl.Audit";
    const string MeterVersion = "0.1.0";

    static readonly string SagaUpdateMessageType = typeof(SagaUpdatedMessage).FullName;
}