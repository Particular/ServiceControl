namespace ServiceControl.Auditing
{
    using System;
    using System.Diagnostics;
    using NuGet.Versioning;
    using Particular.LicensingComponent.AuditThroughput;
    using Particular.LicensingComponent.Contracts;
    using ServiceBus.Management.Infrastructure.Settings;

    // Without this the local audit and audit log queues are counted as customer endpoints in the
    // licensing throughput report, and the report's audit service metadata is blank.
    class PrimaryLocalAuditSource(Settings settings) : ILocalAuditSource
    {
        public bool Enabled => true;

        public RemoteInstanceInformation Describe()
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(PrimaryLocalAuditSource).Assembly.Location).ProductVersion;

            return new RemoteInstanceInformation
            {
                ApiUri = settings.ApiUrl,
                VersionString = version,
                SemanticVersion = SemanticVersion.TryParse(version ?? string.Empty, out var semanticVersion) ? semanticVersion : null,
                Status = "online",
                // Retention is reported as configured. When it is not configured the existing minimum
                // retention gate warns, rather than this guessing a default on the operator's behalf.
                Retention = settings.AuditRetentionPeriod ?? TimeSpan.Zero,
                Queues = [settings.AuditQueue, settings.AuditLogQueue],
                Transport = settings.TransportType
            };
        }
    }
}
