namespace ServiceControl.Persistence
{
    using System;
    using ServiceControl.Infrastructure;

    /// <summary>
    /// Base settings that apply across all Persisters
    /// </summary>
    public abstract class PersistenceSettings
    {
        public bool MaintenanceMode { get; set; }
        //HINT: This needs to be here so that ServerControl instance can add an instance specific metadata to tweak the DatabasePath value
        public string? DatabasePath { get; set; }

        /// <summary>
        /// Whether this host owns the background deletion of data past its retention period. Only one
        /// host in a deployment should, so error ingestion only hosts turn it off.
        /// </summary>
        public bool RunRetentionSweep { get; set; } = true;

        public bool EnableFullTextSearchOnBodies { get; set; } = true;

        public TimeSpan? OverrideCustomCheckRepeatTime { get; set; }

        /// <summary>
        /// Wall-clock limit for the message view queries, see <see cref="QueryTimeLimit" />.
        /// </summary>
        public TimeSpan QueryTimeout { get; set; } = QueryTimeLimit.Default;

        /// <summary>
        /// The setting <see cref="QueryTimeout" /> is read from, as named in the timeout error.
        /// </summary>
        public const string QueryTimeoutSettingName = "ServiceControl/" + QueryTimeLimit.SettingName;
    }
}