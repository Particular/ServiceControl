namespace ServiceControl.Infrastructure;

/// <summary>
/// The meters each instance publishes on. Shared because persisters publish onto the meter their
/// host has already registered with the exporter, and the two assemblies cannot reference each
/// other.
/// </summary>
public static class ServiceControlMeters
{
    public const string Error = "Particular.ServiceControl";
    public const string Audit = "Particular.ServiceControl.Audit";
}
