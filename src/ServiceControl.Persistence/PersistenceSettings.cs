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

        /// <summary>
        /// Base path for storing message bodies on the filesystem.
        /// Initialized by persistence configuration based on DatabasePath or explicit configuration.
        /// </summary>
        public string MessageBodyStoragePath { get; set; }

        /// <summary>
        /// Minimum body size in bytes to trigger compression. Bodies smaller than this threshold
        /// will not be compressed to avoid performance overhead on small payloads.
        /// Default is 4KB (4096 bytes).
        /// </summary>
        public int MinBodySizeForCompression { get; set; } = 4096;

        public TimeSpan? OverrideCustomCheckRepeatTime { get; set; }
    }
}