#nullable enable
namespace ServiceControl.Persistence.RavenDB.Throughput.Indexes;

using System.Linq;
using Raven.Client.Documents.Indexes;
using ServiceControl.Persistence.RavenDB.Throughput.Models;

// Deployed to the throughput database. The licensing queries go through this index, because a dynamic query makes
// RavenDB create an auto index, which has no per-index search engine setting. The field names follow the client
// convention for nested properties, so that a query on EndpointId.Name uses the EndpointId_Name field.
class EndpointIndex : AbstractIndexCreationTask<EndpointDocument>
{
    public EndpointIndex() => Map = endpoints =>
        from endpoint in endpoints
        select new
        {
            EndpointId_ThroughputSource = endpoint.EndpointId.ThroughputSource,
            EndpointId_Name = endpoint.EndpointId.Name,
            endpoint.SanitizedName,
            endpoint.EndpointIndicators,
        };
}
