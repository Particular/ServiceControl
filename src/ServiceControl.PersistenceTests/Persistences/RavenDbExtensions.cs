namespace ServiceControl.PersistenceTests
{
    using System.ComponentModel.Composition.Hosting;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Infrastructure.RavenDB;
    using Microsoft.Extensions.DependencyInjection;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceBus.Management.Infrastructure.Settings;

    static class RavenDbExtensions
    {
        public static async Task<EmbeddableDocumentStore> AddInitializedDocumentStore(this IServiceCollection serviceCollection)
        {
            var settings = new Settings
            {
                RunInMemory = true
            };
            var documentStore = new EmbeddableDocumentStore();
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

            serviceCollection.AddSingleton<IDocumentStore>(documentStore);
            return documentStore;
        }
    }
}