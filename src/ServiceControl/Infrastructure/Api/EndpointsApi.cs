namespace ServiceControl.Infrastructure.Api
{
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;
    using ServiceControl.Persistence;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class EndpointsApi(IEndpointInstanceMonitoring monitoring) : IEndpointsApi
    {
        public async Task<List<Endpoint>> GetEndpoints()
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

            return await Task.FromResult(endpoints);
        }
    }
}
