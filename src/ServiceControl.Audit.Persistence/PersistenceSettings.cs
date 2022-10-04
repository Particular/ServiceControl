namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;

    public class PersistenceSettings
    {
        public PersistenceSettings(
            TimeSpan auditRetentionPeriod,
            bool enableFullTextSearchOnBodies,
            int maxBodySizeToStore)
        {
            AuditRetentionPeriod = auditRetentionPeriod;
            EnableFullTextSearchOnBodies = enableFullTextSearchOnBodies;
            MaxBodySizeToStore = maxBodySizeToStore;

            PersisterSpecificSettings = new Dictionary<string, string>();
        }

        public bool MaintenanceMode { get; set; }

        public TimeSpan AuditRetentionPeriod { get; set; }

        public bool EnableFullTextSearchOnBodies { get; set; }

        public int MaxBodySizeToStore { get; set; }

        public IDictionary<string, string> PersisterSpecificSettings { get; }
    }
}