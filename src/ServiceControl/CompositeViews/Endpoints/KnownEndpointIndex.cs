namespace ServiceControl.CompositeViews.Endpoints
{
    using System.Linq;
    using EndpointControl;
    using Raven.Client.Indexes;

    public class KnownEndpointIndex : AbstractIndexCreationTask<KnownEndpoint>
    {
        public KnownEndpointIndex()
        {
            Map = messages => from message in messages
                              select new KnownEndpoint
                              {
                                  EndpointDetails = message.EndpointDetails,
                                  HostDisplayName = message.HostDisplayName,
                                  MonitorHeartbeat = message.MonitorHeartbeat,
                              };

            DisableInMemoryIndexing = true;
        }
    }

    
}