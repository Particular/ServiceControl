namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;

    public class QueryResult
    {
        protected QueryResult(object results, QueryStatsInfo queryStatsInfo)
        {
            DynamicResults = results;
            QueryStats = queryStatsInfo;
        }

        public object DynamicResults { get; }

        public QueryStatsInfo QueryStats { get; }
    }

    public class QueryResult<TOut> : QueryResult
    {
        public QueryResult(List<TOut> results, QueryStatsInfo queryStatsInfo) : base(results, queryStatsInfo)
        {
            Results = results;
        }

        public QueryResult(IList<TOut> results, QueryStatsInfo queryStatsInfo) : base(results, queryStatsInfo)
        {
            Results = new List<TOut>(results);
        }

        public List<TOut> Results { get; }

        public static QueryResult<TOut> Empty = new QueryResult<TOut>(new List<TOut>(), new QueryStatsInfo(string.Empty, DateTime.MinValue, 0, 0));
    }
}