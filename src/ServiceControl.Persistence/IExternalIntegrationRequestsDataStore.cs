namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.ExternalIntegrations;

    public interface IExternalIntegrationRequestsDataStore
    {
        void Subscribe(Func<object[], CancellationToken, Task> callback);
        Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests, CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}