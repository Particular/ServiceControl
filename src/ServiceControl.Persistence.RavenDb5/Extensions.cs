namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Threading;
    using Newtonsoft.Json.Linq;
    using Raven.Client.Documents.Conventions;
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

        // TODO: This polyfill of RavenDB 3.5 is a guess based loosely on https://github.com/ravendb/ravendb/blob/v3.5/Raven.Client.Lightweight/Document/DocumentConvention.cs#L151
        public static string DefaultFindFullDocumentKeyFromNonStringIdentifier<T>(this DocumentConventions conventions, T id, Type collectionType, bool allowNull)
        {
            if (allowNull && id.Equals(default(T)))
            {
                return null;
            }

            var collectionName = conventions.FindCollectionName(collectionType);
            return $"{collectionName}{conventions.IdentityPartsSeparator}{id}";
        }
    }
}