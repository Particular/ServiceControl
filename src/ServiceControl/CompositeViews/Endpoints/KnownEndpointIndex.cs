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
                                  EndpointDetails_Name = message.EndpointDetails.Name,
                                  EndpointDetails_Host = message.EndpointDetails.Host,
                                  message.HostDisplayName,
                                  message.Monitored,
                                  message.HasTemporaryId,
                              };

            DisableInMemoryIndexing = true;
        }
    }


}