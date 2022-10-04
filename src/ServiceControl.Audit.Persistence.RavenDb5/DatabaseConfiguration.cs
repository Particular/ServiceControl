namespace ServiceControl.Audit.Persistence.RavenDb5
{
    using Sparrow.Json;
    using System.Collections.Generic;
    using System.Linq;
    using System;

    public class DatabaseConfiguration
    {
        public DatabaseConfiguration(string name, int expirationProcessTimerInSeconds, bool enableFullTextSearch)
        {
            Name = name;
            ExpirationProcessTimerInSeconds = expirationProcessTimerInSeconds;
            EnableFullTextSearch = enableFullTextSearch;
        }

        public string Name { get; }
        public int ExpirationProcessTimerInSeconds { get; }
        public bool EnableFullTextSearch { get; }

        public IEnumerable<string> CollectionsToCompress => Enumerable.Empty<string>();
        public bool EnableDocumentCompression => CollectionsToCompress.Any();
        public Func<string, BlittableJsonReaderObject, string> FindClrType { get; }
    }
}
