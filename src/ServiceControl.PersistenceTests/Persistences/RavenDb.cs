namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence.RavenDb;


    // TODO: Combine with logic in PersistenceTestBase
    class RavenDb : TestPersistence
    {
        public override async Task Configure(IServiceCollection services)
        {
            documentStore = await services.AddInitializedDocumentStore();

            // TODO: Make settings initialization similar to AUDIT persistence tests
            var config = PersistenceConfigurationFactory.LoadPersistenceConfiguration(DataStoreConfig.RavenDB35PersistenceTypeFullyQualifiedName);
            var settings = RavenBootstrapper.Settings;//TODO: This strange, RavenBootstrapper.Settings is already initialized due to "AddInitializedDocumentStore"
            //var settings = config.BuildPersistenceSettings(null);
            var instance = config.Create(settings);
            instance.Configure(services);
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

        public override string ToString() => "RavenDB35";

        EmbeddableDocumentStore documentStore;
    }
}