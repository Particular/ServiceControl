namespace ServiceControl.Audit.Monitoring
{
    using System.Linq;
    using Auditing;
    using Raven.Client.Indexes;

    class EndpointsIndex : AbstractIndexCreationTask<ProcessedMessage, EndpointDetails>
    {
        public EndpointsIndex()
        {
            Map = messages => from message in messages
                let sending = (EndpointDetails)message.MessageMetadata["SendingEndpoint"]
                let receiving = (EndpointDetails)message.MessageMetadata["ReceivingEndpoint"]
                from endpoint in new[] {sending, receiving}
                where endpoint != null
                select new EndpointDetails
                {
                    Host = endpoint.Host,
                    HostId = endpoint.HostId,
                    Name = endpoint.Name
                };

            Reduce = results => from result in results
                group result by new {result.Name, result.HostId}
                into grouped
                let first = grouped.First()
                select new EndpointDetails
                {
                    Host = first.Host,
                    HostId = first.HostId,
                    Name = first.Name
                };
        }
    }
}