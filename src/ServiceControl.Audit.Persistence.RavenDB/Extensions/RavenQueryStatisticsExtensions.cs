namespace ServiceControl.Audit.Persistence.RavenDB.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Auditing.MessagesView;
    using Raven.Client.Documents.Session;
    using ServiceControl.Audit.Persistence.Infrastructure;

    static class RavenQueryStatisticsExtensions
    {
        /// <summary>
        /// For a paged or filtered query. The index etag says whether the data moved, the row ids say which
        /// rows this page renders, and <paramref name="query"/> names the question for the case the rows
        /// cannot: a page with no rows contributes no row terms, so without it two filters that both match
        /// nothing, and a page past the end, all share one value.
        /// </summary>
        public static QueryStatsInfo ToPagedQueryStatsInfo<TRow>(this QueryStatistics stats, IEnumerable<TRow> page, Func<TRow, string> id, params (string Name, object Value)[] query)
        {
            // No index etag means no version at all, as on the primary side. The rows here carry only ids,
            // so the etag is the only term covering a change to a field a row renders; without it a
            // validator would stand still while that field moved.
            if (stats.ResultEtag is not { } resultEtag)
            {
                return new QueryStatsInfo(string.Empty, stats.TotalResults);
            }

            var terms = new List<string>(query.Length + 2)
            {
                Term("index", resultEtag),
                Term("total", stats.TotalResults)
            };

            terms.AddRange(query.Select(term => Term(term.Name, term.Value)));

            var row = 0;

            foreach (var item in page)
            {
                terms.Add(Term(string.Concat("row", row++.ToString(CultureInfo.InvariantCulture)), id(item)));
            }

            return new QueryStatsInfo(DeterministicGuid.MakeId(string.Join("|", terms)).ToString(), stats.TotalResults);
        }

        // Length prefixed, so no value can pose as a different set of terms by containing a separator.
        static string Term(string name, object value)
        {
            var text = Format(value);

            return string.Create(CultureInfo.InvariantCulture, $"{name}:{text.Length}:{text}");
        }

        // Mirrors DataVersion.Format on the primary side. Timestamps go in as ticks: their default
        // formatting stops at whole seconds, which would collide two ranges a fraction apart.
        static string Format(object value) => value switch
        {
            null => string.Empty,
            string text => text,
            bool flag => flag.ToString(),
            DateTime timestamp => timestamp.Ticks.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset timestamp => timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            // Any other ToString is not a documented function of the content, so it could pin the
            // version while the data moves and cache a stale page forever.
            _ => throw new ArgumentException($"A version term cannot be built from {value.GetType()}.", nameof(value))
        };
    }
}
