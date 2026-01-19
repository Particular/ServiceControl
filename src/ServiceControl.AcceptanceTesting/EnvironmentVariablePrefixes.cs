namespace ServiceControl.AcceptanceTesting
{
    using System;

    /// <summary>
    /// Provides environment variable prefixes for each ServiceControl instance type.
    /// </summary>
    public static class EnvironmentVariablePrefixes
    {
        public static string GetPrefix(ServiceControlInstanceType instanceType) => instanceType switch
        {
            ServiceControlInstanceType.Primary => "SERVICECONTROL_",
            ServiceControlInstanceType.Audit => "SERVICECONTROL_AUDIT_",
            ServiceControlInstanceType.Monitoring => "MONITORING_",
            _ => throw new ArgumentOutOfRangeException(nameof(instanceType))
        };
    }
}
