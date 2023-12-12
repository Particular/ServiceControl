namespace ServiceControl.Persistence.Infrastructure
{
    public class QueryResult<TOut>(TOut results, QueryStatsInfo queryStatsInfo)
        where TOut : class
    {
        public TOut Results { get; } = results;

        public string InstanceId { get; set; }

        public QueryStatsInfo QueryStats { get; } = queryStatsInfo;

        public static QueryResult<TOut> Empty() => new(null, QueryStatsInfo.Zero);
    }
}