namespace ServiceControl.Persistence.RavenDb
{
    using System.ComponentModel.Composition.Hosting;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Client.Indexes;

    class RavenStartup
    {
        readonly Assembly[] indexAssemblies = new[]
        {
            typeof(RavenBootstrapper).Assembly,
            typeof(CustomChecksIndex).Assembly
        };

        public async Task CreateIndexesAsync(IDocumentStore documentStore)
        {
            var indexProvider = new CompositionContainer(new AggregateCatalog(indexAssemblies.Select(a => new AssemblyCatalog(a))));

            Logger.Info("Index creation started");
            await IndexCreation.CreateIndexesAsync(indexProvider, documentStore)
                .ConfigureAwait(false);
            Logger.Info("Index creation complete");
        }

        static readonly ILog Logger = LogManager.GetLogger<RavenStartup>();
    }
}