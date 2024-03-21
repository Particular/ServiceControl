namespace Particular.ThroughputCollector.Persistence.RavenDb;

using System.Linq;
using Raven.Client.Documents.Indexes;
using Particular.ThroughputCollector.Contracts;

class EndpointIndex : AbstractIndexCreationTask<Endpoint>
{
    public EndpointIndex() => Map = messages =>
        from message in messages
        select new
        {
            message.Id.ThroughputSource,
            message.Id.Name,
        };
}
