namespace ServiceControl.Audit.Monitoring
{
    using System.Linq;
    using Raven.Client.Documents.Indexes;
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
        }
    }
}