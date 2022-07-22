
namespace ServiceControl.Persistence.InMemory
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using ServiceControl.Monitoring;
    using ServiceControl.Persistence;

    class InMemoryMonitoringDataStore : IMonitoringDataStore
    {
        List<InMemoryEndpoint> endpoints;

        public InMemoryMonitoringDataStore()
        {
            endpoints = new List<InMemoryEndpoint>();
        }

        public Task CreateIfNotExists(EndpointDetails endpoint)
        {
            var id = endpoint.GetDeterministicId();

            if (endpoints.All(a => a.Id != id))
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
            var id = endpoint.GetDeterministicId();
            var inMemoryEndpoint = endpoints.FirstOrDefault(a => a.Id == id);
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

        public Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints()
        {
            var result = endpoints.Select(e => new KnownEndpoint
            {
                EndpointDetails = new EndpointDetails
                {
                    Host = e.Host,
                    HostId = e.HostId,
                    Name = e.HostDisplayName
                },
                HostDisplayName = e.HostDisplayName,
                Monitored = e.Monitored
            })
            .ToList();

            return Task.FromResult<IReadOnlyList<KnownEndpoint>>(result);
        }

        public Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored)
        {
            var id = endpoint.GetDeterministicId();
            var inMemoryEndpoint = endpoints.FirstOrDefault(a => a.Id == id);
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

        public Task Setup() => Task.CompletedTask;
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