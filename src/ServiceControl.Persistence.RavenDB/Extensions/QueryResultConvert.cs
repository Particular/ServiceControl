namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using Persistence.Infrastructure;
    using Raven.Client.Documents.Session;

    static class QueryResultConvert
    {
        /// <summary>
        /// Takes the row identity and the query terms rather than defaulting to neither, because a caller
        /// reaching for a one-argument version of this gets a validator that described only the index, and
        /// so will answer "not modified" to every other page and filter over the same one.
        /// </summary>
        public static QueryResult<IList<T>> ToQueryResult<T>(this IList<T> result, QueryStatistics stats, Func<T, string> id, params (string Name, object Value)[] query)
            where T : class =>
            new(result, stats.ToPagedQueryStatsInfo(result, id, query));
    }
}
