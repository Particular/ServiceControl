namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.ExternalIntegrations;

    public interface IExternalIntegrationRequestsDataStore
    {
        void Subscribe(Func<object[], Task> callback);
        Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests);
    }
}