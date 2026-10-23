namespace ServiceControl.RavenDB
{
    public class EmbeddedDatabaseConfiguration(string serverUrl, string dbName, string dbPath, string logPath, string logsMinLevel)
    {
        public string Name { get; } = dbName;
        public string DbPath { get; } = dbPath;
        public string ServerUrl { get; } = serverUrl;
        public string LogPath { get; } = logPath;
        public string LogsMinLevel { get; } = logsMinLevel;

        public bool RunInMemory { get; set; }
    }
}
