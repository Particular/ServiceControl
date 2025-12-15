namespace ServiceControl.Persistence.Sql.Core.Implementation.UnitOfWork;

using System.Threading.Tasks;
using Entities;
using ServiceControl.Persistence;
using ServiceControl.Persistence.UnitOfWork;

class MonitoringIngestionUnitOfWork(IngestionUnitOfWork parent) : IMonitoringIngestionUnitOfWork
{
    public async Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
    {
        var entity = new KnownEndpointEntity
        {
            Id = knownEndpoint.EndpointDetails.GetDeterministicId(),
            EndpointName = knownEndpoint.EndpointDetails.Name,
            HostId = knownEndpoint.EndpointDetails.HostId,
            Host = knownEndpoint.EndpointDetails.Host,
            HostDisplayName = knownEndpoint.HostDisplayName,
            Monitored = knownEndpoint.Monitored
        };

        // Use EF's change tracking for upsert
        var existing = await parent.DbContext.KnownEndpoints.FindAsync(entity.Id);
        if (existing == null)
        {
            parent.DbContext.KnownEndpoints.Add(entity);
        }
        else
        {
            parent.DbContext.Entry(existing).CurrentValues.SetValues(entity);
        }
    }
}
