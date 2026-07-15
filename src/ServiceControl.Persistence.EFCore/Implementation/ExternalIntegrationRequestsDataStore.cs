namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.Hosting;
using ServiceControl.ExternalIntegrations;

public class ExternalIntegrationRequestsDataStore : IExternalIntegrationRequestsDataStore, IHostedService
{
    public void Subscribe(Func<object[], Task> callback) =>
        throw new NotImplementedException();

    public Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests) =>
        throw new NotImplementedException();

    public Task StartAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task StopAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
