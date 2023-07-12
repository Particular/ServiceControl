namespace ServiceControl.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Raven.Client;
    using ServiceControl.ExternalIntegrations;

    class IntegrationEventDataStore : IIntegrationEventDataStore
    {
        readonly IDocumentStore documentStore;

        public IntegrationEventDataStore(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        const string KeyPrefix = "ExternalIntegrationDispatchRequests";

        public async Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                foreach (var dispatchRequest in dispatchRequests)
                {
                    if (dispatchRequest.Id != null)
                    {
                        throw new ArgumentException("Items cannot have their Id property set");
                    }

                    dispatchRequest.Id = KeyPrefix + "/" + Guid.NewGuid();  // TODO: Key is generated to persistence
                    await session.StoreAsync(dispatchRequest)
                        .ConfigureAwait(false);
                }

                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }
    }
}