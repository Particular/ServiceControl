namespace ServiceControl.Persistence
{
    using System;

    /// <summary>
    /// Marker interface used to serialize persister settings in REST API
    /// </summary>
    public abstract class PersistenceSettings
    {
        public bool MaintenanceMode { get; set; }
        //HINT: This needs to be here so that ServerControl instance can add an instance specific metadata to tweak the DatabasePath value
        public string DatabasePath { get; set; }

        public bool EnableFullTextSearchOnBodies { get; set; } = true;

        public TimeSpan? OverrideCustomCheckRepeatTime { get; set; }
    }
}