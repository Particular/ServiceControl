namespace ServiceControl.Persistence
{
    using System;

    /// <summary>
    /// Base settings that apply across all Persisters
    /// </summary>
    public abstract class PersistenceSettings
    {
        public const int DataSpaceRemainingThresholdDefault = 20;
        public const int MinimumStorageLeftRequiredForIngestionDefault = 5;

        public int MinimumStorageLeftRequiredForIngestion { get; set; } = MinimumStorageLeftRequiredForIngestionDefault;

        public int DataSpaceRemainingThreshold { get; set; } = DataSpaceRemainingThresholdDefault;

        public bool MaintenanceMode { get; set; }
        //HINT: This needs to be here so that ServerControl instance can add an instance specific metadata to tweak the DatabasePath value
        public string DatabasePath { get; set; }

        public bool EnableFullTextSearchOnBodies { get; set; } = true;

        public TimeSpan? OverrideCustomCheckRepeatTime { get; set; }
    }
}