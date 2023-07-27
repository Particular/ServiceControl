namespace ServiceControl.PersistenceTests
{
    using System.ComponentModel.Composition.Hosting;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.RavenDb;

    class InMemory : TestPersistence
    {
        public override async Task Configure(IServiceCollection services)
        {
            fallback = await AddInitializedDocumentStore(services, RavenDb.CreateSettings());

            var config = PersistenceConfigurationFactory.LoadPersistenceConfiguration(DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName);
            var settings = config.BuildPersistenceSettings(null); // TODO: Inmemory might also require settings
            var instance = config.Create(settings);
            instance.Configure(services);
        }

        public override Task CleanupDatabase()
        {
            fallback?.Dispose();
            return base.CleanupDatabase();
        }

        public override string ToString() => "In-Memory";

        EmbeddableDocumentStore fallback;

        static async Task<EmbeddableDocumentStore> AddInitializedDocumentStore(IServiceCollection serviceCollection, PersistenceSettings settings)
        {
            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings);
            documentStore.Initialize();

            ExportProvider CreateIndexProvider(System.Collections.Generic.List<Assembly> indexAssemblies) =>
                new CompositionContainer(
                    new AggregateCatalog(
                        from indexAssembly in indexAssemblies select new AssemblyCatalog(indexAssembly)
                    )
                );

            var indexProvider = CreateIndexProvider(new System.Collections.Generic.List<Assembly> { typeof(RavenBootstrapper).Assembly, typeof(CustomChecksIndex).Assembly });
            await IndexCreation.CreateIndexesAsync(indexProvider, documentStore);

            serviceCollection.AddSingleton<IDocumentStore>(documentStore);
            return documentStore;
        }
    }
}