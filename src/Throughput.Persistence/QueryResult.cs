namespace Throughput.Persistence;

public class QueryResult
{
    protected QueryResult(object? results, QueryStatsInfo queryStatsInfo)
    {
        DynamicResults = results;
        QueryStats = queryStatsInfo;
    }

    public object? DynamicResults { get; }

    public QueryStatsInfo QueryStats { get; }
}

public class QueryResult<TOut>(TOut? results, QueryStatsInfo queryStatsInfo) : QueryResult(results, queryStatsInfo)
    where TOut : class
{
    public TOut? Results { get; } = results;

    public static QueryResult<TOut> Empty() => new(null, QueryStatsInfo.Zero);
}