namespace ServiceControl.Persistence.Infrastructure
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Why an instance contributed nothing to a query.
    /// </summary>
    public enum QueryFailure
    {
        /// <summary>The query ran out of its allowed query time.</summary>
        TimedOut,
        /// <summary>The instance could not be reached.</summary>
        Unavailable,
        /// <summary>The instance answered with an error.</summary>
        Failed
    }

    /// <summary>
    /// An instance whose data a composite result is missing.
    /// </summary>
    public sealed record IncompleteInstance(string InstanceId, QueryFailure Reason);

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

        /// <summary>
        /// Why this instance contributed nothing. Null when it answered, also when it answered with no data.
        /// </summary>
        public QueryFailure? Failure { get; init; }

        /// <summary>
        /// The instances a composite result is missing. Empty when every instance answered.
        /// </summary>
        public IReadOnlyList<IncompleteInstance> IncompleteInstances { get; init; } = [];

        public static QueryResult<TOut> Empty() => new(null, QueryStatsInfo.Zero);

        public static QueryResult<TOut> Failed(QueryFailure reason) => new(null, QueryStatsInfo.Zero) { Failure = reason };

        public static implicit operator Task<QueryResult<TOut>>(QueryResult<TOut> instance) => Task.FromResult(instance);
    }
}