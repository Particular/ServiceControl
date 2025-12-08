namespace ServiceControl.Persistence.Sql;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence;

class NoOpExternalIntegrationRequestsDataStore : IExternalIntegrationRequestsDataStore
{
    public void Subscribe(Func<object[], Task> callback)
    {
    }

    public Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests) =>
        Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
