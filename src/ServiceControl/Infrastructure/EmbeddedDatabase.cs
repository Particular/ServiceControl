namespace ServiceControl.Infrastructure
{
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    static class EmbeddedDatabase
    {
        public static void Start(ServiceBus.Management.Infrastructure.Settings.Settings settings, LoggingSettings loggingSettings)
        {
            var serverOptions = new ServerOptions
            {
                AcceptEula = true,
                DataDirectory = settings.DbPath,
                LogsPath = loggingSettings.LogPath
            };
            EmbeddedServer.Instance.StartServer(serverOptions);
        }

        public static Task<IDocumentStore> GetSCDatabase()
        {
            return EmbeddedServer.Instance.GetDocumentStoreAsync("servicecontrol");
        }
    }
}