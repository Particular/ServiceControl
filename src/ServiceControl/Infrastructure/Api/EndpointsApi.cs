namespace ServiceControl.Infrastructure.Api
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;
    using ServiceControl.Persistence;

    class EndpointsApi(IEndpointInstanceMonitoring monitoring) : IEndpointsApi
    {
        public Task<List<Endpoint>> GetEndpoints(CancellationToken cancellationToken)
        {
            var endpoints = new List<Endpoint>();
            foreach (var endpointsView in monitoring.GetEndpoints())
            {
                endpoints.Add(new Endpoint
                {
                    Id = endpointsView.Id,
                    HostDisplayName = endpointsView.HostDisplayName,
                    IsSendingHeartbeats = endpointsView.IsSendingHeartbeats,
                    Monitored = endpointsView.Monitored,
                    MonitorHeartbeat = endpointsView.MonitorHeartbeat,
                    Name = endpointsView.Name
                });
            }

            return Task.FromResult(endpoints);
        }
    }
}