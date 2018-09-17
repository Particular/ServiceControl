namespace ServiceControl.CompositeViews.Messages
{
    class QueryResult
    {
        protected QueryResult(object results, QueryStatsInfo queryStatsInfo)
        {
            DynamicResults = results;
            QueryStats = queryStatsInfo;
        }


        public object DynamicResults { get; }
        public string InstanceId { get; set; }

        public QueryStatsInfo QueryStats { get; }
    }

    class QueryResult<TOut> : QueryResult
        where TOut : class
    {
        public QueryResult(TOut results, QueryStatsInfo queryStatsInfo) : base(results, queryStatsInfo)
        {
            Results = results;
        }

        public TOut Results { get; }

        public static QueryResult<TOut> Empty() => new QueryResult<TOut>(null, QueryStatsInfo.Zero);
    }
}