#nullable enable
namespace ServiceControl.Persistence.RavenDB.Throughput.Indexes;

using System.Linq;
using Raven.Client.Documents.Indexes;
using ServiceControl.Persistence.RavenDB.Throughput.Models;

class EndpointIndex : AbstractIndexCreationTask<EndpointDocument>
{
    public EndpointIndex() => Map = messages =>
        from message in messages
        select new
        {
            message.EndpointId.ThroughputSource,
            message.EndpointId.Name,
        };
}