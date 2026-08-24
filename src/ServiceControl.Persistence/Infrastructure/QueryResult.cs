namespace ServiceControl.Persistence.Infrastructure
{
    using System.Threading.Tasks;

    public class QueryResult<TOut>(TOut? results, QueryStatsInfo queryStatsInfo)
        where TOut : class
    {
        public TOut? Results { get; } = results;

        public string? InstanceId { get; set; }

        /// <summary>
        /// The result the scatter-gather got from its own instance.
        /// </summary>
        public bool IsLocalInstance { get; set; }

        public QueryStatsInfo QueryStats { get; } = queryStatsInfo;

        public static QueryResult<TOut> Empty() => new(null, QueryStatsInfo.Zero);

        public static implicit operator Task<QueryResult<TOut>>(QueryResult<TOut> instance) => Task.FromResult(instance);
    }
}