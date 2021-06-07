namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Raven.Client;
    using Raven.Client.Embedded;

    class EmbeddedRavenDbHostedService : IHostedService
    {
        readonly EmbeddableDocumentStore documentStore;
        readonly Func<IDocumentStore, Task> storeInitializer;

        public EmbeddedRavenDbHostedService(EmbeddableDocumentStore documentStore, Func<IDocumentStore, Task> storeInitializer)
        {
            this.documentStore = documentStore;
            this.storeInitializer = storeInitializer;
        }

        public async Task StartAsync(CancellationToken cancellationToken) =>
            // HINT: Currently we are handed an initialized documentStore
            //documentStore.Initialize();
            await storeInitializer(documentStore)
                .ConfigureAwait(false);

        public Task StopAsync(CancellationToken cancellationToken)
        {
            documentStore.Dispose();
            return Task.CompletedTask;
        }
    }
}