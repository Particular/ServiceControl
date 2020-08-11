using NServiceBus;
using NServiceBus.Features;
using Raven.Client;
using ServiceControl.Audit.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceControl.Audit.Infrastructure.RavenDB
{
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
            
            using (var session = store.OpenAsyncSession())
            {
                var endpointsFromIndex = await session.Query<dynamic>(knownEndpointsIndex.Name, true).ToListAsync().ConfigureAwait(false);
                var knownEndpoints = endpointsFromIndex.Select(endpoint => new KnownEndpoint
                {
                    Id = KnownEndpoint.MakeDocumentId(endpoint.Name, Guid.Parse(endpoint.HostId)),
                    Host = endpoint.Host,
                    HostId = Guid.Parse(endpoint.HostId),
                    Name = endpoint.Name,
                    LastSeen = DateTime.Now // We don't store it anywhere so....?
                });

                foreach (var endpoint in knownEndpoints)
                {
                    await session.StoreAsync(endpoint).ConfigureAwait(false);
                }

                await session.SaveChangesAsync().ConfigureAwait(false);
            }

            await store.AsyncDatabaseCommands.DeleteIndexAsync(knownEndpointsIndex.Name).ConfigureAwait(false);
        }

        async Task WaitForNonStaleIndex(string indexName)
        {
            var isStale = true;
            do
            {
                var stats = await store.AsyncDatabaseCommands.GetStatisticsAsync().ConfigureAwait(false);
                isStale = stats.StaleIndexes.Any(index => index == indexName);

                if (isStale)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            } while (isStale);
        }

        protected override Task OnStop(IMessageSession session)
        {
            return Task.CompletedTask;
        }
    }
}
