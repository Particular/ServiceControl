namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;

    class RavenStartup
    {
        public async Task CreateIndexesAsync(IDocumentStore documentStore)
        {
            Logger.Info("Index creation started");
            await IndexCreation.CreateIndexesAsync(typeof(RavenBootstrapper).Assembly, documentStore);
            Logger.Info("Index creation complete");
        }

        static readonly ILog Logger = LogManager.GetLogger<RavenStartup>();
    }
}