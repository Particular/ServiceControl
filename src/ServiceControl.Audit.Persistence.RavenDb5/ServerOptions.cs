namespace ServiceControl.Audit.Persistence.RavenDb5
{
    public class ServerOptions
    {
        public ServerOptions(string connectionString)
        {
            UseEmbeddedServer = false;
            ConnectionString = connectionString;
        }
        public ServerOptions(string dbPath, string maintenanceUrl)
        {
            UseEmbeddedServer = true;
            DbPath = dbPath;
            MaintenanceUrl = maintenanceUrl;
        }

        public string ConnectionString { get; }
        public bool UseEmbeddedServer { get; }
        public string DbPath { get; }
        public string MaintenanceUrl { get; }
    }
}
