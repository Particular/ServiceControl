namespace ServiceControl.Persistence
{
    using System;
    using ServiceControl.Infrastructure;

    /// <summary>
    /// Base settings that apply across all Persisters
    /// </summary>
    public abstract class PersistenceSettings
    {
        /// <summary>
        /// Whether this host starts the database and nothing else, so an operator can repair or inspect it.
        /// Only the RavenDB persister supports it. There the embedded server and RavenDB Studio start as
        /// usual, and the host registers nothing that ingests or serves data. On any other persister the
        /// host refuses to start in maintenance mode.
        /// </summary>
        public bool MaintenanceMode { get; set; }

        /// <summary>
        /// The directory that holds the database files, or null when this host holds none. The RavenDB
        /// persister fills it in from its DbPath setting, and its disk space checks measure the drive that
        /// this path names. The SQL persisters leave it null, because their files live on the database server.
        /// </summary>
        public string? DatabasePath { get; set; }

        /// <summary>
        /// Whether this host deletes data that is past its retention period. Only one host in a deployment
        /// must do this, so a host that only ingests errors turns it off. The SQL persisters start a
        /// background sweeper when it is true. RavenDB expires documents on its own and does not read this.
        /// </summary>
        public bool RunRetentionSweep { get; set; } = true;

        /// <summary>
        /// Whether message bodies are indexed for search. The RavenDB persister decides this as it ingests a
        /// message, so a change only reaches messages ingested after it. The SQL persisters always index the
        /// body, so the value does not change what they do.
        /// </summary>
        public bool EnableFullTextSearchOnBodies { get; set; } = true;

        /// <summary>
        /// How many completed retry operations the history keeps, copied from the ServiceControl/RetryHistoryDepth
        /// setting. At 0 it keeps none, so the next retry to complete clears the history.
        /// </summary>
        public int RetryHistoryDepth { get; set; }

        public TimeSpan? OverrideCustomCheckRepeatTime { get; set; }

        /// <summary>
        /// The wall clock limit for a message query, see <see cref="QueryTimeLimit" />. It is also how long
        /// this instance waits for a remote instance to answer.
        /// </summary>
        public TimeSpan QueryTimeout { get; set; } = QueryTimeLimit.Default;

        /// <summary>
        /// The name of the setting that <see cref="QueryTimeout" /> comes from. The timeout error names it, so
        /// an operator can see which setting to change.
        /// </summary>
        public const string QueryTimeoutSettingName = "ServiceControl/" + QueryTimeLimit.SettingName;
    }
}