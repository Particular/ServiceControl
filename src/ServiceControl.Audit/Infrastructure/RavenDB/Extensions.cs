namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    //using System;
    //using System.Threading;
    //using Raven.Client.Documents.Queries;

    static class Extensions
    {
        // TODO: RAVEN5 - Put this back in and figure out how to make it work
        //public static void Query<TState>(this DocumentDatabase db, string index, IndexQuery query, CancellationToken externalCancellationToken, Action<RavenJObject, TState> onItem, TState state)
        //{
        //    var results = db.Queries.Query(index, query, externalCancellationToken);
        //    foreach (var doc in results.Results)
        //    {
        //        onItem(doc, state);
        //    }
        //}
    }
}