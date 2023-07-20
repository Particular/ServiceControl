namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    class RavenDb : TestPersistence
    {
        public override async Task Configure(IServiceCollection services)
        {
            documentStore = await services.AddInitializedDocumentStore();
            services.AddServiceControlPersistence(DataStoreType.RavenDB35);
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