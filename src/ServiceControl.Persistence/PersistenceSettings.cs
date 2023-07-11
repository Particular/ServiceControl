namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;

    public class PersistenceSettings
    {
        public PersistenceSettings(
            TimeSpan errorRetentionPeriod,
            TimeSpan? auditRetentionPeriod,
            bool maintenanceMode
            )
        {
            ErrorRetentionPeriod = errorRetentionPeriod;
            AuditRetentionPeriod = auditRetentionPeriod;
            MaintenanceMode = maintenanceMode;
        }

        public IDictionary<string, string> PersisterSpecificSettings { get; } = new Dictionary<string, string>();

        public bool MaintenanceMode { get; }
        public TimeSpan ErrorRetentionPeriod { get; }
        public TimeSpan? AuditRetentionPeriod { get; }
    }
}