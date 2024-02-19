namespace Throughput.Persistence;

using System;
using System.Collections.Generic;

public class PersistenceSettings(
    TimeSpan auditRetentionPeriod,
    bool enableFullTextSearchOnBodies,
    int maxBodySizeToStore)
{
    public bool MaintenanceMode { get; set; }

    public TimeSpan AuditRetentionPeriod { get; set; } = auditRetentionPeriod;

    public bool EnableFullTextSearchOnBodies { get; set; } = enableFullTextSearchOnBodies;

    public int MaxBodySizeToStore { get; set; } = maxBodySizeToStore;

    public IDictionary<string, string> PersisterSpecificSettings { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}