namespace Throughput.Persistence.RavenDb;

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

    public string ConnectionString { get; } = string.Empty;

    public bool UseEmbeddedServer { get; }
    public string DbPath { get; } = string.Empty;
    public string ServerUrl { get; } = string.Empty;
    public string LogPath { get; } = string.Empty;
    public string LogsMode { get; } = string.Empty;
}
