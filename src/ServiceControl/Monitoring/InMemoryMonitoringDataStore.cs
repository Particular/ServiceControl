
namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using ServiceControl.Infrastructure;

    class InMemoryMonitoringDataStore : IMonitoringDataStore
    {
        readonly EndpointInstanceMonitoring monitoring;
        List<InMemoryEndpoint> endpoints;

        public InMemoryMonitoringDataStore(EndpointInstanceMonitoring monitoring)
        {
            this.monitoring = monitoring;
            endpoints = new List<InMemoryEndpoint>();
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task CreateIfNotExists(EndpointDetails endpoint)
        {
            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            if (!endpoints.Any(a => a.Id == id))
            {
                endpoints.Add(new InMemoryEndpoint
                {
                    Id = id,
                    HostId = endpoint.HostId,
                    Host = endpoint.Host,
                    HostDisplayName = endpoint.Name,
                    Monitored = true
                });
            }
        }

        public async Task CreateOrUpdate(EndpointDetails endpoint)
        {
            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            var inMemoryEndpoint = endpoints.Where(a => a.Id == id).FirstOrDefault();
            if (inMemoryEndpoint == null)
            {
                endpoints.Add(new InMemoryEndpoint
                {
                    Id = id,
                    HostId = endpoint.HostId,
                    Host = endpoint.Host,
                    HostDisplayName = endpoint.Name,
                    Monitored = true
                });
            }
            else
            {
                inMemoryEndpoint.Monitored = monitoring.IsMonitored(id);
            }
        }

        public Task Delete(Guid endpointId)
        {
            var endpoint = endpoints.FirstOrDefault(e => e.Id == endpointId);
            if (endpoint != null)
            {
                endpoints.Remove(endpoint);
            }

            return Task.CompletedTask;
        }

        public async Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored)
        {
            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            var inMemoryEndpoint = endpoints.Where(a => a.Id == id).FirstOrDefault();
            if (inMemoryEndpoint != null)
            {
                inMemoryEndpoint.Monitored = monitoring.IsMonitored(id);
            }
        }

        public async Task WarmupMonitoringFromPersistence()
        {
            if (endpoints != null)
            {
                endpoints.ForEach(e =>
                {
                    monitoring.DetectEndpointFromPersistentStore(new EndpointDetails
                    {
                        HostId = e.HostId,
                        Host = e.Host,
                        Name = e.HostDisplayName
                    }, e.Monitored);
                });
            }
        }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }

    class InMemoryEndpoint
    {
        public Guid Id { get; set; }
        public Guid HostId { get; set; }
        public string Host { get; set; }
        public string HostDisplayName { get; set; }
        public bool Monitored { get; set; }
    }
}