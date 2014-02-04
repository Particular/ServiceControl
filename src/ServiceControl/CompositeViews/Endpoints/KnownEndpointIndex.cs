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
                              select new
                              {
                                  message.Id,
                                  message.Name,
                                  message.HostDisplayName,
                                  MonitorHeartbeats = message.MonitorHeartbeat,
                              };
        }
    }

    
}