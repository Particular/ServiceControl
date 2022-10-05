namespace ServiceControl.Audit.Persistence.RavenDb5
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Sparrow.Json;

    public class DatabaseConfiguration
    {
        public DatabaseConfiguration(string name, int expirationProcessTimerInSeconds, bool enableFullTextSearch, ServerOptions serverOptions)
        {
            Name = name;
            ExpirationProcessTimerInSeconds = expirationProcessTimerInSeconds;
            EnableFullTextSearch = enableFullTextSearch;
            ServerOptions = serverOptions;
        }

        public string Name { get; }

        public int ExpirationProcessTimerInSeconds { get; }

        public bool EnableFullTextSearch { get; }

        public IEnumerable<string> CollectionsToCompress => Enumerable.Empty<string>();

        public bool EnableDocumentCompression => CollectionsToCompress.Any();

        public Func<string, BlittableJsonReaderObject, string> FindClrType { get; }

        public ServerOptions ServerOptions { get; }
    }
}
