namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Threading;
    using Raven.Abstractions.Data;
    using Raven.Database;
    using Raven.Json.Linq;

    public static class Extensions
    {
        public static void Query(this DocumentDatabase db, string index, IndexQuery query, CancellationToken externalCancellationToken, Action<RavenJObject> onItem)
        {
            var results = db.Queries.Query(index, query, externalCancellationToken);
            foreach (var doc in results.Results)
            {
                onItem(doc);
            }
        }
    }
}