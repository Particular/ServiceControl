namespace ServiceControl.Audit.Monitoring
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.Monitoring;

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
                    message.HasTemporaryId
                };

            DisableInMemoryIndexing = true;
        }
    }
}