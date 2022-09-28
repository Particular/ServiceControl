namespace ServiceControl.Audit.Infrastructure.Migration
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client;
    using Raven.Abstractions.Data;
    using ServiceControl.Audit.Persistence.Monitoring;

    class MigrateKnownEndpoints : IDataMigration
    {
        IDocumentStore documentStore;
        public MigrateKnownEndpoints(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }
        public async Task Migrate(int pageSize = 1024)
        {
            var knownEndpointsIndex = await documentStore.AsyncDatabaseCommands.GetIndexAsync("EndpointsIndex").ConfigureAwait(false);
            if (knownEndpointsIndex == null)
            {
                Logger.Debug("EndpointsIndex migration already completed.");
                // Index has already been deleted, no need to migrate
                return;
            }

            var dbStatistics = await documentStore.AsyncDatabaseCommands.GetStatisticsAsync().ConfigureAwait(false);
            var indexStats = dbStatistics.Indexes.First(index => index.Name == knownEndpointsIndex.Name);
            if (indexStats.Priority == IndexingPriority.Disabled)
            {
                Logger.Debug("EndpointsIndex already disabled. Deleting EndpointsIndex.");

                // This should only happen the second time the migration is attempted.
                // The index is disabled so the data should have been migrated. We can now delete the index.
                await documentStore.AsyncDatabaseCommands.DeleteIndexAsync(knownEndpointsIndex.Name).ConfigureAwait(false);
                return;
            }

            int previouslyDone = 0;
            do
            {
                using (var session = documentStore.OpenAsyncSession())
                {
                    var endpointsFromIndex = await session.Query<dynamic>(knownEndpointsIndex.Name, true)
                        .Skip(previouslyDone)
                        .Take(pageSize)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (endpointsFromIndex.Count == 0)
                    {
                        Logger.Debug("No more records from EndpointsIndex to migrate.");
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

                    using (var bulkInsert = documentStore.BulkInsert(options: new BulkInsertOptions
                    {
                        OverwriteExisting = true
                    }))
                    {
                        foreach (var endpoint in knownEndpoints)
                        {
                            bulkInsert.Store(endpoint);
                        }

                        Logger.Debug($"Migrating {endpointsFromIndex.Count} entries.");
                        await bulkInsert.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            while (true);

            Logger.Debug("EndpointsIndex entries migrated. Disabling EndpointsIndex.");
            // Disable the index so it can be safely deleted in the next migration run
            await documentStore.AsyncDatabaseCommands.SetIndexPriorityAsync(knownEndpointsIndex.Name, IndexingPriority.Disabled).ConfigureAwait(false);

        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(MigrateKnownEndpoints));
    }
}