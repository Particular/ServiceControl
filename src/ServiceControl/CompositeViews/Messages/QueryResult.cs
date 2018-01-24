namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Collections.Generic;

    public class QueryResult
    {
        public QueryResult(List<MessagesView> messages, QueryStatsInfo queryStatsInfo)
        {
            Messages = messages;
            QueryStats = queryStatsInfo;
        }

        public QueryResult(IList<MessagesView> messages, QueryStatsInfo queryStatsInfo)
        {
            Messages = new List<MessagesView>(messages);
            QueryStats = queryStatsInfo;
        }

        public List<MessagesView> Messages { get; }
        public QueryStatsInfo QueryStats { get; set; }

        public static QueryResult Empty = new QueryResult(new List<MessagesView>(), new QueryStatsInfo(string.Empty, DateTime.MinValue, 0, 0));

    }
}