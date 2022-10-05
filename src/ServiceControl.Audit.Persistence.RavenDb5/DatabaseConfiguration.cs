namespace ServiceControl.Audit.Persistence.RavenDb5
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Sparrow.Json;

    public class DatabaseConfiguration
    {
        public DatabaseConfiguration(
            string name,
            int expirationProcessTimerInSeconds,
            bool enableFullTextSearch,
            TimeSpan auditRetentionPeriod,
            int maxBodySizeToStore,
            ServerConfiguration serverConfiguration)
        {
            Name = name;
            ExpirationProcessTimerInSeconds = expirationProcessTimerInSeconds;
            EnableFullTextSearch = enableFullTextSearch;
            AuditRetentionPeriod = auditRetentionPeriod;
            MaxBodySizeToStore = maxBodySizeToStore;
            ServerConfiguration = serverConfiguration;
        }

        public string Name { get; }

        public int ExpirationProcessTimerInSeconds { get; }

        public bool EnableFullTextSearch { get; }

        public IEnumerable<string> CollectionsToCompress => Enumerable.Empty<string>();

        public bool EnableDocumentCompression => CollectionsToCompress.Any();

        public Func<string, BlittableJsonReaderObject, string> FindClrType { get; }

        public ServerConfiguration ServerConfiguration { get; }

        public TimeSpan AuditRetentionPeriod { get; }

        public int MaxBodySizeToStore { get; }
    }
}
