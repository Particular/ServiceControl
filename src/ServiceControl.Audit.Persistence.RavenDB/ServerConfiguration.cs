namespace ServiceControl.Audit.Persistence.RavenDB
{
    public class ServerConfiguration
    {
        public ServerConfiguration(string connectionString)
        {
            UseEmbeddedServer = false;
            ConnectionString = connectionString;
        }

        public ServerConfiguration(string dbPath, string serverUrl, string logPath, string logsMode)
        {
            UseEmbeddedServer = true;
            DbPath = dbPath;
            ServerUrl = serverUrl;
            LogPath = logPath;
            LogsMode = logsMode;
        }

        public string ConnectionString { get; }
        public string ClientCertificateBase64 { get; internal set; }
        public bool UseEmbeddedServer { get; }
        public string DbPath { get; internal set; } //Setter for ATT only
        public string ServerUrl { get; }
        public string LogPath { get; }
        public string LogsMode { get; set; }
    }
}
