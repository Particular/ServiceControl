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
                Id = endpoint.Id,
                Name = endpoint.Name,
                Machines = endpoint.Machines,
                IsEmittingHeartbeats = false
            }));


            AddMap<Heartbeat>(endpoints => endpoints.Select(endpoint => new
            {
                Id = endpoint.OriginatingEndpoint,
                Name = (string)null,
                Machines = (List<string>)null,
                IsEmittingHeartbeats = true
            }));

            Reduce = results => from message in results
                                group message by message.Id
                                    into g
                                    select new EndpointsView
                                    {
                                        Id = g.Key,
                                        Name = g.Single(r => r.Name != null).Name,
                                        Machines = g.Single(r => r.Machines != null).Machines,
                                        IsEmittingHeartbeats = g.Any(r=>r.IsEmittingHeartbeats),
                                    };


           
        }

 
    }
    public class EndpointsView
    {
      

        public string Id { get; set; }

        public string Name { get; set; }

        public List<string> Machines { get; set; }

        public bool IsEmittingHeartbeats { get; set; }
    }

}