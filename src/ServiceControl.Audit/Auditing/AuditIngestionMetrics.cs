namespace ServiceControl.Audit.Auditing;

using System.Diagnostics.Metrics;

public class AuditIngestionMetrics
{
    public AuditIngestionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, MeterVersion);
        forwardedMessagesCounter = meter.CreateCounter<long>(CreateInstrumentName("forwarded"), description: "Audit ingestion forwarded message count");
    }

    public void IncrementMessagesForwarded(int count) => forwardedMessagesCounter.Add(count);

    static string CreateInstrumentName(string instrumentName) => $"sc.audit.ingestion.{instrumentName}".ToLower();

    readonly Counter<long> forwardedMessagesCounter;

    const string MeterName = "Particular.ServiceControl.Audit";
    const string MeterVersion = "0.1.0";
}