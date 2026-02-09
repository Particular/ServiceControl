namespace ServiceControl.Audit.Auditing.Metrics;

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using EndpointPlugin.Messages.SagaState;
using Infrastructure.Settings;
using NServiceBus;
using NServiceBus.Transport;
using ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

public class IngestionMetrics
{
    public const string MeterName = "Particular.ServiceControl.Audit";

    public static readonly string BatchDurationInstrumentName = $"{InstrumentPrefix}.batch_duration";
    public static readonly string MessageDurationInstrumentName = $"{InstrumentPrefix}.message_duration";
    public static readonly string CapacityInstrumentName = $"{InstrumentPrefix}.capacity";

    public IngestionMetrics(IMeterFactory meterFactory, IngestionThrottleState throttleState, Settings settings)
    {
        this.throttleState = throttleState;
        this.settings = settings;

        var meter = meterFactory.Create(MeterName, MeterVersion);

        batchDuration = meter.CreateHistogram<double>(BatchDurationInstrumentName, unit: "s", "Message batch processing duration in seconds");
        ingestionDuration = meter.CreateHistogram<double>(MessageDurationInstrumentName, unit: "s", description: "Audit message processing duration in seconds");
        consecutiveBatchFailureGauge = meter.CreateObservableGauge($"{InstrumentPrefix}.consecutive_batch_failures_total", () => consecutiveBatchFailures, description: "Consecutive audit ingestion batch failures");
        failureCounter = meter.CreateCounter<long>($"{InstrumentPrefix}.failures_total", description: "Audit ingestion failure count");
        capacityGauge = meter.CreateObservableGauge(CapacityInstrumentName, GetCapacity, unit: "percent", description: "Current ingestion capacity (100 = full, lower during cleanup)");
    }

    int GetCapacity() => throttleState.GetCapacityPercent(
        settings.AuditIngestionMaxParallelWriters,
        settings.CleanupThrottleInterval,
        settings.MinWritersDuringCleanup);

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

    long consecutiveBatchFailures;

    readonly IngestionThrottleState throttleState;
    readonly Settings settings;
    readonly Histogram<double> batchDuration;
#pragma warning disable IDE0052
    // this can be changed to Gauge<T> once we can use the latest version of System.Diagnostics.DiagnosticSource
    readonly ObservableGauge<long> consecutiveBatchFailureGauge;
    readonly ObservableGauge<int> capacityGauge;
#pragma warning restore IDE0052
    readonly Histogram<double> ingestionDuration;
    readonly Counter<long> failureCounter;

    const string MeterVersion = "0.1.0";
    const string InstrumentPrefix = "sc.audit.ingestion";

    static readonly string SagaUpdateMessageType = typeof(SagaUpdatedMessage).FullName;
}