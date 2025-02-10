namespace ServiceControl.Audit;

using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

static class Telemetry
{
    const string MeterName = "Particular.ServiceControl.Audit";
    public static readonly Meter Meter = new(MeterName, "0.1.0");

    public static string CreateInstrumentName(string instrumentNamespace, string instrumentName) => $"sc.audit.{instrumentNamespace}.{instrumentName}".ToLower();

    public static void AddAuditIngestionMeters(this MeterProviderBuilder builder)
    {
        builder.AddMeter(MeterName);
    }
}