namespace ServiceControl.Audit;

using System.Diagnostics.Metrics;

static class AuditMetrics
{
    public const string MeterName = "Particular.ServiceControl";
    public static readonly Meter Meter = new(MeterName, "0.1.0");
    public static readonly string Prefix = "particular.servicecontrol.audit";
}