namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Raven.Client.Embedded;

    class EmbeddedRavenDbHostedService : IHostedService
    {
        readonly EmbeddableDocumentStore documentStore;

        public EmbeddedRavenDbHostedService(EmbeddableDocumentStore documentStore)
            => this.documentStore = documentStore;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // HINT: Currently we are handed an initialized documentStore
            //documentStore.Initialize();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            documentStore.Dispose();
            return Task.CompletedTask;
        }
    }
}