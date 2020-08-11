namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.Audit.Monitoring;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    class MigrateKnownEndpoints : Feature
    {
        public MigrateKnownEndpoints()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b => new KnownEndpointMigrator(b.Build<IDocumentStore>()));
        }
    }

    class KnownEndpointMigrator : FeatureStartupTask
    {
        IDocumentStore store;

        public KnownEndpointMigrator(IDocumentStore store)
        {
            this.store = store;
        }

        protected override async Task OnStart(IMessageSession messageSession)
        {
            var knownEndpointsIndex = await store.AsyncDatabaseCommands.GetIndexAsync("EndpointsIndex").ConfigureAwait(false);
            if (knownEndpointsIndex == null)
            {
                return;
            }

            await WaitForNonStaleIndex(knownEndpointsIndex.Name).ConfigureAwait(false);

            int previouslyDone = 0;
            do {
                using (var session = store.OpenAsyncSession())
                {
                    var endpointsFromIndex = await session.Query<dynamic>(knownEndpointsIndex.Name, true)
                        .Take(1024)
                        .Skip(previouslyDone)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (endpointsFromIndex.Count == 0)
                    {
                        previouslyDone = -1;
                    }
                    else
                    {
                        previouslyDone += endpointsFromIndex.Count;

                        var knownEndpoints = endpointsFromIndex.Select(endpoint => new KnownEndpoint
                        {
                            Id = KnownEndpoint.MakeDocumentId(endpoint.Name, Guid.Parse(endpoint.HostId)),
                            Host = endpoint.Host,
                            HostId = Guid.Parse(endpoint.HostId),
                            Name = endpoint.Name,
                            LastSeen = DateTime.UtcNow // Set the imported date to be now
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
                        }

                        await session.SaveChangesAsync().ConfigureAwait(false);
                    }
                }
            } while (previouslyDone > 0);

            await store.AsyncDatabaseCommands.DeleteIndexAsync(knownEndpointsIndex.Name).ConfigureAwait(false);
        }

        async Task WaitForNonStaleIndex(string indexName)
        {
            var maxWaitTime = DateTime.UtcNow.AddHours(1);
            var isStale = true;
            do
            {
                var stats = await store.AsyncDatabaseCommands.GetStatisticsAsync().ConfigureAwait(false);
                isStale = stats.StaleIndexes.Any(index => index == indexName);

                if (isStale)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            } while (isStale && DateTime.UtcNow <= maxWaitTime);
        }

        protected override Task OnStop(IMessageSession session)
        {
            return Task.CompletedTask;
        }
    }
}
