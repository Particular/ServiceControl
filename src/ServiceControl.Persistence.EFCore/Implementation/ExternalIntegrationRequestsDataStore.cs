namespace ServiceControl.Persistence.EFCore.Implementation;

using Microsoft.Extensions.Hosting;
using ServiceControl.ExternalIntegrations;

public class ExternalIntegrationRequestsDataStore : IExternalIntegrationRequestsDataStore, IHostedService
{
    public void Subscribe(Func<object[], Task> callback)
    {
        //todo:
    }


    public Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests) =>
        throw new NotImplementedException();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        //todo:
        return Task.CompletedTask;
    }


    public Task StopAsync(CancellationToken cancellationToken)
    {
        //todo:
        return Task.CompletedTask;
    }

}
