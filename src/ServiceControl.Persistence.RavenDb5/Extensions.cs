namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Threading;
    using Newtonsoft.Json.Linq;
    using Raven.Client.Documents.Queries;

    static class Extensions
    {
        public static void Query<TState>(this DocumentDatabase db, string index, IndexQuery query, Action<JObject, TState> onItem, TState state, CancellationToken cancellationToken = default)
        {
            var results = db.Queries.Query(index, query, cancellationToken);
            foreach (var doc in results.Results)
            {
                onItem(doc, state);
            }
        }
    }
}