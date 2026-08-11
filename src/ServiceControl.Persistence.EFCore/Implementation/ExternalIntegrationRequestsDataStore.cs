namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.Hosting;
using ServiceControl.ExternalIntegrations;

public class ExternalIntegrationRequestsDataStore : IExternalIntegrationRequestsDataStore, IHostedService
{
    public void Subscribe(Func<object[], CancellationToken, Task> callback) { }

    public Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
