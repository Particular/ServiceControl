namespace ServiceControl.Audit.Infrastructure
{
    using Raven.Embedded;
    using Settings;

    static class EmbeddedDatabase
    {
        public static void Start(Settings.Settings settings, LoggingSettings loggingSettings)
        {
            var serverOptions = new ServerOptions
            {
                AcceptEula = true,
                DataDirectory = settings.DbPath,
                LogsPath = loggingSettings.LogPath
            };
            EmbeddedServer.Instance.StartServer(serverOptions);
        }
    }
}