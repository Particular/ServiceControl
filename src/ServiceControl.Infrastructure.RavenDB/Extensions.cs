namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Threading;
    using Raven.Abstractions.Data;
    using Raven.Database;
    using Raven.Json.Linq;

    public static class Extensions
    {
        public static void Query<TState>(this DocumentDatabase db, string index, IndexQuery query, Action<RavenJObject, TState> onItem, TState state, CancellationToken cancellationToken)
        {
            var results = db.Queries.Query(index, query, cancellationToken);
            foreach (var doc in results.Results)
            {
                onItem(doc, state);
            }
        }
    }
}