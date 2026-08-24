namespace ServiceControl.Persistence.Infrastructure
{
    using System.Collections.Generic;

    public static class VersionedRowExtensions
    {
        /// <summary>
        /// Creates a QueryStatsInfo from a collection of rows, and versions it over those rows.
        /// </summary>
        public static QueryStatsInfo ToQueryStatsInfo<TRow>(this IReadOnlyCollection<TRow> rows, string name, long totalCount)
            where TRow : IVersionedRow => new(DataVersion.OverRows([(name, totalCount)], rows), totalCount);
    }
}
