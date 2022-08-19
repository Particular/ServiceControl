namespace ServiceControl.Audit.Persistence.RavenDB.Expiration
{
    using System.Linq;
    using Monitoring;
    using Raven.Client.Indexes;

    public class ExpiryKnownEndpointsIndex : AbstractIndexCreationTask<KnownEndpoint>
    {
        public ExpiryKnownEndpointsIndex()
        {
            Map = knownEndpoints => from knownEndpoint in knownEndpoints
                                    select new
                                    {
                                        LastSeen = knownEndpoint.LastSeen.Ticks
                                    };

            // we expect a lowish number of endpoints so that can run in memory
            DisableInMemoryIndexing = false;
        }
    }
}