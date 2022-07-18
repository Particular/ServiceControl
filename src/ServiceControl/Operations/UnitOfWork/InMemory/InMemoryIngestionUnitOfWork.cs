namespace ServiceControl.Operations
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Monitoring;

    class InMemoryIngestionUnitOfWork : IIngestionUnitOfWork
    {
        ConcurrentBag<KnownEndpoint> knownEndpoints = new ConcurrentBag<KnownEndpoint>();
        IMonitoringDataStore dataStore;

        public InMemoryIngestionUnitOfWork(IMonitoringDataStore dataStore)
        {
            this.dataStore = dataStore;
            Monitoring = new InMemoryMonitoringIngestionUnitOfWork(this);
        }

        public IMonitoringIngestionUnitOfWork Monitoring { get; }
        public IRecoverabilityIngestionUnitOfWork Recoverability { get; }

        public async Task Complete()
        {
            foreach (var endpoint in knownEndpoints)
            {
                await dataStore.CreateIfNotExists(endpoint.EndpointDetails)
                    .ConfigureAwait(false);
            }
        }

        internal void AddEndpoint(KnownEndpoint knownEndpoint) => knownEndpoints.Add(knownEndpoint);
    }
}