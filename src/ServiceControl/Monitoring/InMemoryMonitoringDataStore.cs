
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
        List<InMemoryEndpoint> endpoints;

        public InMemoryMonitoringDataStore()
        {
            endpoints = new List<InMemoryEndpoint>();
        }

        public Task CreateIfNotExists(EndpointDetails endpoint)
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
                    Monitored = false
                });
            }

            return Task.CompletedTask;
        }

        public Task CreateOrUpdate(EndpointDetails endpoint, EndpointInstanceMonitoring endpointInstanceMonitoring)
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
                inMemoryEndpoint.Monitored = endpointInstanceMonitoring.IsMonitored(id);
            }

            return Task.CompletedTask;
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
        public Task<KnownEndpoint[]> GetAllKnownEndpoints() =>
            Task.FromResult((from e in endpoints
                             select new KnownEndpoint
                             {
                                 EndpointDetails = new EndpointDetails
                                 {
                                     Host = e.Host,
                                     HostId = e.HostId,
                                     Name = e.HostDisplayName
                                 },
                                 HostDisplayName = e.HostDisplayName,
                                 Monitored = e.Monitored
                             }).ToArray());

        public Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored)
        {
            var id = DeterministicGuid.MakeId(endpoint.Name, endpoint.HostId.ToString());

            var inMemoryEndpoint = endpoints.Where(a => a.Id == id).FirstOrDefault();
            if (inMemoryEndpoint != null)
            {
                inMemoryEndpoint.Monitored = isMonitored;
            }

            return Task.CompletedTask;
        }

        public Task WarmupMonitoringFromPersistence(EndpointInstanceMonitoring endpointInstanceMonitoring)
        {
            if (endpoints != null)
            {
                endpoints.ForEach(e =>
                {
                    endpointInstanceMonitoring.DetectEndpointFromPersistentStore(new EndpointDetails
                    {
                        HostId = e.HostId,
                        Host = e.Host,
                        Name = e.HostDisplayName
                    }, e.Monitored);
                });
            }

            return Task.CompletedTask;
        }
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