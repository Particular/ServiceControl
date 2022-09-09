namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;

    public class PersistenceSettings
    {
        public PersistenceSettings(TimeSpan auditRetentionPeriod)
        {
            AuditRetentionPeriod = auditRetentionPeriod;
            PersisterSpecificSettings = new Dictionary<string, string>();
        }

        public bool IsSetup { get; set; }

        public bool MaintenanceMode { get; set; }

        public TimeSpan AuditRetentionPeriod { get; }

        public IDictionary<string, string> PersisterSpecificSettings { get; }
    }
}