namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using NServiceBus.Installation;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.Audit.Monitoring;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    class MigrateKnownEndpoints : INeedToInstallSomething
    {
        IDocumentStore store;

        public MigrateKnownEndpoints(IDocumentStore store)
        {
            this.store = store;
        }

        public Task Install(string identity)
        {
            return MigrateEndpoints();
        }

        internal async Task MigrateEndpoints(int pageSize = 1024)
        {
            var knownEndpointsIndex = await store.AsyncDatabaseCommands.GetIndexAsync("EndpointsIndex").ConfigureAwait(false);
            if (knownEndpointsIndex == null)
            {
                return;
            }

            var dbStatistics = await store.AsyncDatabaseCommands.GetStatisticsAsync().ConfigureAwait(false);
            var indexStats = dbStatistics.Indexes.First(index => index.Name == knownEndpointsIndex.Name);
            if (indexStats.Priority == IndexingPriority.Disabled)
            {
                await store.AsyncDatabaseCommands.DeleteIndexAsync(knownEndpointsIndex.Name).ConfigureAwait(false);
                return;
            }

            int previouslyDone = 0;
            do
            {
                using (var session = store.OpenAsyncSession())
                {
                    var endpointsFromIndex = await session.Query<dynamic>(knownEndpointsIndex.Name, true)
                        .Skip(previouslyDone)
                        .Take(pageSize)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (endpointsFromIndex.Count == 0)
                    {
                        break;
                    }
                     
                    previouslyDone += endpointsFromIndex.Count;

                    var knownEndpoints = endpointsFromIndex.Select(endpoint => new KnownEndpoint
                    {
                        Id = KnownEndpoint.MakeDocumentId(endpoint.Name, Guid.Parse(endpoint.HostId)),
                        Host = endpoint.Host,
                        HostId = Guid.Parse(endpoint.HostId),
                        Name = endpoint.Name,
                        LastSeen = DateTime.UtcNow // Set the imported date to be now since we have no better guess
                    });

                    using (var bulkInsert = store.BulkInsert(options: new BulkInsertOptions
                    {
                        OverwriteExisting = true
                    }))
                    {
                        foreach (var endpoint in knownEndpoints)
                        {
                            bulkInsert.Store(endpoint);
                        }

                        await bulkInsert.DisposeAsync().ConfigureAwait(false);
                    }
                }
            } while (true);

            await store.AsyncDatabaseCommands.SetIndexPriorityAsync(knownEndpointsIndex.Name, IndexingPriority.Disabled).ConfigureAwait(false);
        }
    }
}
