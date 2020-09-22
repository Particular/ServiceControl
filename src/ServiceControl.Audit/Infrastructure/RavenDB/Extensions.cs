namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using System;
    using System.Threading;
    using Raven.Abstractions.Data;
    using Raven.Client.Documents.Queries;
    using Raven.Database;
    using Raven.Json.Linq;

    static class Extensions
    {
        public static void Query<TState>(this DocumentDatabase db, string index, IndexQuery query, CancellationToken externalCancellationToken, Action<RavenJObject, TState> onItem, TState state)
        {
            var results = db.Queries.Query(index, query, externalCancellationToken);
            foreach (var doc in results.Results)
            {
                onItem(doc, state);
            }
        }
    }
}