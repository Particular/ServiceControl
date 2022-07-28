namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Database;
    using Raven.Json.Linq;

    public static class Extensions
    {
        static readonly ILog Log = LogManager.GetLogger(typeof(Extensions));
        public static async Task TestAllIndexesAndResetIfException(this IDocumentStore store)
        {
            foreach (var index in store.DatabaseCommands.GetStatistics().Indexes)
            {
                try
                {
                    using (var session = store.OpenAsyncSession())
                    {
                        await session.Advanced.AsyncDocumentQuery<object>(index.Name).Take(1).ToListAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    Log.Info("Resetting errored index: " + index.Name);
                    store.DatabaseCommands.ResetIndex(index.Name);
                }
            }
        }
        public static void Query<TState>(this DocumentDatabase db, string index, IndexQuery query, Action<RavenJObject, TState> onItem, TState state, CancellationToken cancellationToken = default)
        {
            var results = db.Queries.Query(index, query, cancellationToken);
            foreach (var doc in results.Results)
            {
                onItem(doc, state);
            }
        }
    }
}