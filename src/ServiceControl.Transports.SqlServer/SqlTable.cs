#nullable enable
namespace ServiceControl.Transports.SqlServer
{
    using System.Collections.Generic;
    using System.Linq;

    class SqlTable(string name, string schema, string? catalog)
    {
        // Unquoted identifier parts, exposed so the bulk catalog-view query (see QueueLengthProvider)
        // can group tables by catalog and match rows from sys.schemas / sys.tables back to the
        // tracked tables without parsing the composed full name.
        public string UnquotedName { get; } = NameHelper.Unquote(name);
        public string UnquotedSchema { get; } = NameHelper.Unquote(schema);
        public string? UnquotedCatalog { get; } = catalog == null ? null : NameHelper.Unquote(catalog);

        readonly string _fullTableName = BuildFullTableName(name, schema, catalog);

        static string BuildFullTableName(string name, string schema, string? catalog) =>
            catalog == null
                ? $"{NameHelper.Quote(schema)}.{NameHelper.Quote(name)}"
                : $"{NameHelper.Quote(catalog)}.{NameHelper.Quote(schema)}.{NameHelper.Quote(name)}";

        public override string ToString() =>
            _fullTableName;

        // Builds a SINGLE query that returns the (approximate) length of every supplied table in one
        // catalog, read entirely from the system catalog views (sys.partitions/sys.tables/sys.schemas).
        //
        // Why this is better than the previous per-table IF EXISTS + max-min(RowVersion) probe:
        //   * One statement covers N queues instead of N statements (the customer in case 00105882 saw
        //     thousands of statements/min; this collapses them to one per catalog per poll).
        //   * sys.partitions reports the row count maintained by the engine, so it never reads, scans or
        //     locks the queue tables themselves — the IF EXISTS guard and the max-min RowVersion scan are
        //     both gone. Metadata visibility means SELECT permission on the queue tables is enough; no
        //     VIEW DATABASE STATE is required (unlike the dm_db_partition_stats DMV).
        //   * The queue tables are heaps (index_id 0) with non-clustered indexes only, so index_id IN (0,1)
        //     yields the table's row count.
        //
        // The result is still an approximation — comparable to the existing max-min(RowVersion) estimate,
        // which itself over-counts identity gaps — which is acceptable for the queue-length monitoring graph.
        public static string BuildBulkLengthQuery(string? catalog, IReadOnlyCollection<SqlTable> tables)
        {
            var prefix = catalog == null ? string.Empty : $"{NameHelper.Quote(catalog)}.";

            // With no tables the predicate would be empty, producing the invalid `AND ()`. Emit a
            // constant-false predicate instead so the query stays valid and simply returns no rows.
            var predicate = tables.Count == 0
                ? "1 = 0"
                : string.Join(
                    "\n     OR ",
                    tables.Select(t => $"(s.name = '{Escape(t.UnquotedSchema)}' AND t.name = '{Escape(t.UnquotedName)}')"));

            // NOLOCK hints (statement-scoped) rather than SET TRANSACTION ISOLATION LEVEL READ
            // UNCOMMITTED: the latter is connection-scoped and would persist on the pooled physical
            // connection. The hint keeps the dirty-read scope on this single catalog-view read.
            return $"""
                    SELECT s.name AS TableSchema, t.name AS TableName, SUM(p.rows) AS [RowCount]
                    FROM {prefix}sys.partitions p WITH (NOLOCK)
                    INNER JOIN {prefix}sys.tables t WITH (NOLOCK) ON t.object_id = p.object_id
                    INNER JOIN {prefix}sys.schemas s WITH (NOLOCK) ON s.schema_id = t.schema_id
                    WHERE p.index_id IN (0, 1)
                      AND ({predicate})
                    GROUP BY s.name, t.name;
                    """;
        }

        static string Escape(string identifier) => identifier.Replace("'", "''");

        public static SqlTable Parse(string address, string defaultSchema)
        {
            var parts = address.Split('@');

            return new SqlTable(
                name: parts[0],
                schema: parts.Length > 1 ? parts[1] : defaultSchema,
                catalog: parts.Length > 2 ? parts[2] : null
            );
        }

        protected bool Equals(SqlTable other) =>
            string.Equals(_fullTableName, other._fullTableName);

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((SqlTable)obj);
        }

        public override int GetHashCode() =>
            _fullTableName.GetHashCode();
    }
}