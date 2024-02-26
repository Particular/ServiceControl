namespace Particular.ThroughputCollector.Persistence.RavenDb;

using System.Linq;
using Raven.Client.Documents.Indexes;

class EndpointIndex : AbstractIndexCreationTask<Endpoint>
{
    public EndpointIndex()
    {
        Map = messages =>

            from message in messages
            select new
            {
                message.ThroughputSource,
                message.Name,
                message.Queue
            };
    }
}
