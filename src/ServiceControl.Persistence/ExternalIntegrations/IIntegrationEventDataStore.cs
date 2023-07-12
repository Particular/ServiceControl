namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.ExternalIntegrations;

    public interface IIntegrationEventDataStore
    {
        Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests);
    }
}

