namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Configuration;

    public sealed class PersistenceSettings(
        TimeSpan auditRetentionPeriod,
        bool enableFullTextSearchOnBodies,
        int maxBodySizeToStore
    )
    {
        public bool MaintenanceMode { get; set; }
        public TimeSpan AuditRetentionPeriod { get; set; } = auditRetentionPeriod;
        public bool EnableFullTextSearchOnBodies { get; set; } = enableFullTextSearchOnBodies;
        public int MaxBodySizeToStore { get; set; } = maxBodySizeToStore;
    }
}