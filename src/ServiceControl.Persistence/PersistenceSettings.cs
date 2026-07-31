namespace ServiceControl.Persistence
{
    using System;

    /// <summary>
    /// Base settings that apply across all Persisters
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