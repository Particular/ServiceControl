namespace ServiceControl.Infrastructure.RavenDB
{
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.RavenDb;

    class PurgeKnownEndpointsWithTemporaryIdsThatAreDuplicateDataMigration : IDataMigration
    {
        public Task Migrate(IDocumentStore store)
        {
            using (var session = store.OpenSession())
            {
                var endpoints = session.Query<KnownEndpoint, KnownEndpointIndex>().ToList();

                foreach (var knownEndpoints in endpoints.GroupBy(e => e.EndpointDetails.Host + e.EndpointDetails.Name))
                {
                    var fixedIdsCount = knownEndpoints.Count(e => !e.HasTemporaryId);

                    //If we have knowEndpoints with non temp ids, we should delete all temp ids ones.
                    if (fixedIdsCount > 0)
                    {
                        knownEndpoints.Where(e => e.HasTemporaryId)
                            .ForEach(k =>
                            {
                                string documentId = RavenDbMonitoringDataStore.MakeDocumentId(k.EndpointDetails.GetDeterministicId());
                                store.DatabaseCommands.Delete(documentId, null);
                            });
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}