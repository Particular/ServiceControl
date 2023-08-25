namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;

    public class PersistenceSettings
    {
        public PersistenceSettings(
            TimeSpan errorRetentionPeriod,
            TimeSpan eventsRetentionPeriod,
            TimeSpan? auditRetentionPeriod,
            int externalIntegrationsDispatchingBatchSize,
            bool maintenanceMode
            )
        {
            ErrorRetentionPeriod = errorRetentionPeriod;
            EventsRetentionPeriod = eventsRetentionPeriod;
            AuditRetentionPeriod = auditRetentionPeriod;
            MaintenanceMode = maintenanceMode;
            ExternalIntegrationsDispatchingBatchSize = externalIntegrationsDispatchingBatchSize;
        }

        public IDictionary<string, string> PersisterSpecificSettings { get; } = new Dictionary<string, string>();

        public bool MaintenanceMode { get; }
        public TimeSpan ErrorRetentionPeriod { get; }
        public TimeSpan EventsRetentionPeriod { get; }
        public TimeSpan? AuditRetentionPeriod { get; }
        public int ExternalIntegrationsDispatchingBatchSize { get; }
    }
}