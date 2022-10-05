namespace ServiceControl.Audit.Persistence.RavenDb5
{
    public class ServerConfiguration
    {
        public ServerConfiguration(string connectionString)
        {
            UseEmbeddedServer = false;
            ConnectionString = connectionString;
        }
        public ServerConfiguration(string dbPath, string serverUrl)
        {
            UseEmbeddedServer = true;
            DbPath = dbPath;

            ServerUrl = serverUrl;
        }

        public string ConnectionString { get; }
        public bool UseEmbeddedServer { get; }
        public string DbPath { get; }
        public string ServerUrl { get; }
    }
}
