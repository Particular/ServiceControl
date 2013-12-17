namespace ServiceControl.CompositeViews.Endpoints
{
    using System.Collections.Generic;
    using System.Linq;
    using EndpointControl;
    using HeartbeatMonitoring;
    using Raven.Client.Indexes;

    public class EndpointsViewIndex : AbstractMultiMapIndexCreationTask<EndpointsView>
    {
        public EndpointsViewIndex()
        {
            AddMap<KnownEndpoint>(endpoints => endpoints.Select(endpoint => new
            {
                endpoint.Id,
                endpoint.Name,
                endpoint.Machines,
                EndpointProperties = new Dictionary<string, string> { { "monitored", "true" } }
            }));

            AddMap<Heartbeat>(endpoints => endpoints.Select(endpoint => new
            {
                Id = endpoint.OriginatingEndpoint.Name,
                Name = endpoint.OriginatingEndpoint.Name,
                Machines = new List<string> { endpoint.OriginatingEndpoint.Machine },
                EndpointProperties = new Dictionary<string, string> { { "emits_heartbeats", "true" } }
            }));

            Reduce = results => from message in results
                                group message by message.Id
                                    into g
                                    select new EndpointsView
                                    {
                                        Id = g.Key,
                                        Name = g.First().Name,
                                        Machines = g.SelectMany(r => r.Machines).Distinct().ToList(),
                                        EndpointProperties = g.SelectMany(r => r.EndpointProperties).ToList()
                                    };
        }
    }

    public class EndpointsView
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string> Machines { get; set; }
        public List<KeyValuePair<string, string>> EndpointProperties { get; set; }
    }

}