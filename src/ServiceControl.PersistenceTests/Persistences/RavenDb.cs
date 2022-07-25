namespace ServiceControl.PersistenceTests
{
    using System.ComponentModel.Composition.Hosting;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Infrastructure.RavenDB;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Persistence.Tests;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;

    class RavenDb : PersistenceDataStoreFixture
    {
        public override async Task SetupDataStore()
        {
            var settings = new Settings
            {
                RunInMemory = true
            };
            documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings);
            documentStore.Initialize();

            ExportProvider CreateIndexProvider(System.Collections.Generic.List<Assembly> indexAssemblies) =>
                new CompositionContainer(
                    new AggregateCatalog(
                        from indexAssembly in indexAssemblies select new AssemblyCatalog(indexAssembly)
                    )
                );

            var indexProvider = CreateIndexProvider(new System.Collections.Generic.List<Assembly> { typeof(RavenBootstrapper).Assembly });
            await IndexCreation.CreateIndexesAsync(indexProvider, documentStore)
                .ConfigureAwait(false);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IDocumentStore>(documentStore);
            serviceCollection.AddServiceControlPersistence(DataStoreType.RavenDb);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            MonitoringDataStore = serviceProvider.GetRequiredService<IMonitoringDataStore>();
            CustomCheckDataStore = serviceProvider.GetRequiredService<ICustomChecksDataStore>();
        }

        public override Task CompleteDBOperation()
        {
            documentStore.WaitForIndexing();
            return base.CompleteDBOperation();
        }

        public override Task CleanupDB()
        {
            documentStore?.Dispose();
            return base.CleanupDB();
        }

        public override string ToString() => "RavenDb";

        EmbeddableDocumentStore documentStore;
    }
}