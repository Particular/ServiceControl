namespace ServiceControl.Infrastructure;

/// <summary>
/// The meters each instance publishes on. These name the instance, not the subject: everything the
/// primary publishes shares <see cref="Primary"/>, including audit ingestion when the primary hosts
/// it. What a measurement is about is carried by the instrument prefix instead. Shared because
/// persisters publish onto the meter their host has already registered with the exporter, and the
/// two assemblies cannot reference each other.
/// </summary>
public static class ServiceControlMeters
{
    public const string Primary = "Particular.ServiceControl";
    public const string AuditInstance = "Particular.ServiceControl.Audit";
}
