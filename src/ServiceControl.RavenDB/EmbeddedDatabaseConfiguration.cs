namespace ServiceControl.RavenDB
{
    public class EmbeddedDatabaseConfiguration(string serverUrl, string dbName, string dbPath, string logPath, string logsMode)
    {
        public string Name { get; } = dbName;
        public string DbPath { get; } = dbPath;
        public string ServerUrl { get; } = serverUrl;
        public string LogPath { get; } = logPath;
        public string LogsMode { get; } = logsMode;

        public bool RunInMemory { get; set; }

        // Makes RavenDB throw on a query that would create an auto index, instead of creating it. Tests use it to catch
        // dynamic queries, and the migration source uses it because creating an index would be a write.
        public bool DisableAutoIndexes { get; set; }
    }
}
