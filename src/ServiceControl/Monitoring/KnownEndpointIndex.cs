namespace ServiceControl.Audit.Monitoring
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.Monitoring;

    public class KnownEndpointIndex : AbstractIndexCreationTask<KnownEndpoint>
    {
        public KnownEndpointIndex()
        {
            Map = knownEndpoints => from knownEndpoint in knownEndpoints
                                    select new
                                    {
                                        EndpointDetails_Name = knownEndpoint.EndpointDetails.Name,
                                        EndpointDetails_Host = knownEndpoint.EndpointDetails.Host,
                                        knownEndpoint.HostDisplayName,
                                        knownEndpoint.Monitored,
                                        knownEndpoint.HasTemporaryId
                                    };

            DisableInMemoryIndexing = true;
        }
    }
}