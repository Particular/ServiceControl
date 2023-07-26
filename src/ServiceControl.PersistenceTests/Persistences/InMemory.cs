namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    class InMemory : TestPersistence
    {
        public override async Task Configure(IServiceCollection services)
        {
            fallback = await services.AddInitializedDocumentStore();

            var config = PersistenceConfigurationFactory.LoadPersistenceConfiguration(DataStoreConfig.InMemoryPersistenceTypeFullyQualifiedName);
            var settings = config.BuildPersistenceSettings(null); // TODO: Inmemory might also require settings
            var instance = config.Create(settings);
            instance.Configure(services);
        }

        public override Task CleanupDB()
        {
            fallback?.Dispose();
            return base.CleanupDB();
        }

        public override string ToString() => "In-Memory";

        EmbeddableDocumentStore fallback;
    }
}