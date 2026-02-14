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

            PersisterSpecificSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public bool MaintenanceMode { get; set; }

        public TimeSpan AuditRetentionPeriod { get; set; }

        public bool EnableFullTextSearchOnBodies { get; set; }

        /// <summary>
        /// Base path for storing message bodies on the filesystem.
        /// Initialized by persistence configuration based on DatabasePath or explicit configuration.
        /// </summary>
        public string MessageBodyStoragePath { get; set; }

        public string MessageBodyStorageConnectionString { get; set; }

        public int MaxBodySizeToStore { get; set; }

        public IDictionary<string, string> PersisterSpecificSettings { get; }
    }
}