namespace ServiceControl.Audit;

using System.Diagnostics.Metrics;

static class Telemetry
{
    public const string MeterName = "Particular.ServiceControl.Audit";
    public static readonly Meter Meter = new(MeterName, "0.1.0");
}