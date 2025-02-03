namespace ServiceControl.Audit;

using System.Diagnostics.Metrics;

static class AuditMetrics
{
    public static readonly Meter Meter = new("Particular.ServiceControl", "0.1.0");
    public static readonly string Prefix = "particular.servicecontrol.audit";
}