namespace ServiceControl.Persistence.Infrastructure
{
    using System.Threading.Tasks;

    public class QueryResult<TOut>(TOut results, QueryStatsInfo queryStatsInfo)
        where TOut : class
    {
        public TOut Results { get; } = results;

        public string InstanceId { get; set; }

        public QueryStatsInfo QueryStats { get; } = queryStatsInfo;

        /// <summary>
        /// The caller already holds this version, so <see cref="Results"/> was never fetched and is
        /// <c>null</c>. <see cref="QueryStats"/> is still populated.
        /// </summary>
        public bool NotModified { get; private init; }

        public static QueryResult<TOut> Empty() => new(null, QueryStatsInfo.Zero);

        public static QueryResult<TOut> Unchanged(QueryStatsInfo queryStatsInfo) =>
            new(null, queryStatsInfo) { NotModified = true };

        public static implicit operator Task<QueryResult<TOut>>(QueryResult<TOut> instance) => Task.FromResult(instance);
    }
}