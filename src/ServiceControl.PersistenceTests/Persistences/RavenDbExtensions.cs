namespace ServiceControl.PersistenceTests
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence.RavenDb;
    using Raven.Client;
    using Raven.Client.Embedded;
    using Raven.Client.Indexes;
    using ServiceControl.Persistence;

    static class RavenDbExtensions
    {
        public static async Task<EmbeddableDocumentStore> AddInitializedDocumentStore(this IServiceCollection serviceCollection)
        {
            var retentionPeriod = TimeSpan.FromMinutes(1);
            var settings = new PersistenceSettings(retentionPeriod, retentionPeriod, retentionPeriod, 100, false)
            {
                PersisterSpecificSettings = { [RavenBootstrapper.RunInMemoryKey] = bool.TrueString }
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

            var indexProvider = CreateIndexProvider(new System.Collections.Generic.List<Assembly> { typeof(RavenBootstrapper).Assembly, typeof(CustomChecksIndex).Assembly });
            await IndexCreation.CreateIndexesAsync(indexProvider, documentStore);

            serviceCollection.AddSingleton<IDocumentStore>(documentStore);
            return documentStore;
        }
    }
}