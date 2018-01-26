namespace ServiceControl.CompositeViews.Messages
{
    public class QueryResult
    {
        protected QueryResult(object results, string instanceId, QueryStatsInfo queryStatsInfo)
        {
            InstanceId = instanceId;
            DynamicResults = results;
            QueryStats = queryStatsInfo;
        }


        public object DynamicResults { get; }
        public string InstanceId { get; }

        public QueryStatsInfo QueryStats { get; }
    }

    public class QueryResult<TOut> : QueryResult
        where TOut: class 
    {
        public QueryResult(TOut results, string instanceId, QueryStatsInfo queryStatsInfo) : base(results, instanceId, queryStatsInfo)
        {
            Results = results;
        }

        public TOut Results { get; }

        public static QueryResult<TOut> Empty(string instanceId) => new QueryResult<TOut>(null, instanceId, QueryStatsInfo.Zero);
    }
}