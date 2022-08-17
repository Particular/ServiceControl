namespace ServiceControl.Audit.Persistence.Tests
{
    using System.ComponentModel.Composition.Hosting;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceControl.Audit.Infrastructure.RavenDB;
    using ServiceControl.Audit.Infrastructure.Settings;

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