namespace ServiceControl.Persistence.RavenDB
{
    using System.Collections.Generic;
    using Persistence.Infrastructure;
    using Raven.Client.Documents.Session;

    static class QueryResultConvert
    {
        public static QueryResult<IList<T>> ToQueryResult<T>(this IList<T> result, QueryStatistics stats)
            where T : class
        {
            return new QueryResult<IList<T>>(result, stats.ToQueryStatsInfo());
        }
    }
}