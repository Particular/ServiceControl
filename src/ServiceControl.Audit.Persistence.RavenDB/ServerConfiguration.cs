namespace ServiceControl.Audit.Persistence.RavenDB
{
    using ServiceControl.RavenDB;

    public class ServerConfiguration : IRavenClientCertificateInfo
    {
        public ServerConfiguration(string connectionString)
        {
            UseEmbeddedServer = false;
            ConnectionString = connectionString;
        }

        public ServerConfiguration(string dbPath, string serverUrl, string logPath, string logsMinLevel)
        {
            UseEmbeddedServer = true;
            DbPath = dbPath;
            ServerUrl = serverUrl;
            LogPath = logPath;
            LogsMinLevel = logsMinLevel;
        }

        public string ConnectionString { get; }
        public string ClientCertificatePath { get; set; }
        public string ClientCertificateBase64 { get; set; }
        public string ClientCertificatePassword { get; set; }
        public bool UseEmbeddedServer { get; }
        public string DbPath { get; internal set; } //Setter for ATT only
        public string ServerUrl { get; }
        public string LogPath { get; }
        public string LogsMinLevel { get; set; }
    }
}
