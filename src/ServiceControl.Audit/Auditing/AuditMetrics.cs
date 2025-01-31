namespace ServiceControl.Audit.Auditing;

using System.Diagnostics.Metrics;

static class AuditMetrics
{
    public static readonly Meter Meter = new("ServiceControl", "0.1.0");
    public static readonly string Prefix = "particular.servicecontrol.audit";
}