namespace ServiceControl.Infrastructure.RavenDB
{
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Commands;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.RavenDb;

    // TODO: I don't know if we can delete this because no prior Raven5 database will exist, or if it's an ongoing need to purge these things on every startup
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
                        foreach (var key in knownEndpoints.Where(e => e.HasTemporaryId))
                        {
                            string documentId = RavenDbMonitoringDataStore.MakeDocumentId(key.EndpointDetails.GetDeterministicId());
                            session.Advanced.RequestExecutor.Execute(new DeleteDocumentCommand(documentId, null), session.Advanced.Context);
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}