namespace ServiceControl.Transports.SqlServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Microsoft.Extensions.Logging;

    class QueueLengthProvider : AbstractQueueLengthProvider
    {
        public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store, ILogger<QueueLengthProvider> logger) : base(settings, store)
        {
            connectionString = ConnectionString
                .RemoveCustomConnectionStringParts(out var customSchema, out _)
                .RemoveQueueLengthQueryDelayInterval(out var configuredInterval)
                .RemoveQueueLengthQueryMaxDelayInterval(out var configuredMaxInterval);

            baseDelay = configuredInterval ?? DefaultQueryDelayInterval;
            // Adaptive back-off is ON by default: while every monitored queue is idle the cadence ramps from
            // the base interval up to this ceiling. An operator can widen or effectively disable it (set equal
            // to the base) via QueueLengthQueryMaxDelayInterval. Never let it fall below the base.
            maxDelay = configuredMaxInterval ?? DefaultQueryMaxDelayInterval;
            if (maxDelay < baseDelay)
            {
                maxDelay = baseDelay;
            }
            currentDelay = baseDelay;

            defaultSchema = customSchema ?? "dbo";
            this.logger = logger;

            logger.LogInformation("SQL queue length query interval: base {BaseDelay}, max {MaxDelay} (adaptive back-off {State})",
                baseDelay, maxDelay, maxDelay > baseDelay ? "enabled" : "disabled");
        }
        public override void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
            var sqlTable = SqlTable.Parse(queueToTrack.InputQueue, defaultSchema);

            tableNames.AddOrUpdate(queueToTrack, _ => sqlTable, (_, currentSqlTable) =>
            {
                if (!currentSqlTable.Equals(sqlTable))
                {
                    tableSizes.TryRemove(currentSqlTable, out var _);
                }

                return sqlTable;
            });

            tableSizes.TryAdd(sqlTable, 0);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Time the whole iteration so the pacing delay below can subtract the work already done this
                // cycle. The effective cadence is then max(currentDelay, iterationDuration) — the query
                // overlaps the wait instead of being added to it. That additive query-time term is what let
                // the cadence drift past the 1s monitoring bucket and starve buckets of samples, producing the
                // #4556 false-zero "sawtooth"; a slow query (> interval) simply paces at its own duration.
                var iterationStart = Stopwatch.GetTimestamp();

                try
                {
                    await QueryTableSizes(stoppingToken);

                    UpdateQueueLengthStore();

                    // Adapt the cadence: full speed while any queue has work, exponential back-off while the
                    // whole system is idle. Backing off only when EVERY queue is empty keeps the fix for
                    // issue #4556 intact — the false-zero "sawtooth" only affects non-empty queues, and those
                    // are always sampled at the base interval here. Computed after the query (not before) so a
                    // snap-back to the base interval takes effect on THIS iteration's pacing rather than one
                    // backed-off wait later.
                    var maxObservedLength = tableSizes.IsEmpty ? 0 : tableSizes.Values.Max();
                    currentDelay = NextDelay(currentDelay, baseDelay, maxDelay, maxObservedLength);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error querying sql queue sizes");
                }

                // Pace AFTER the try so a failed query is still throttled by the interval. Otherwise a fast,
                // persistent failure (bad login, denied SELECT) would spin the loop with no delay, hammering
                // the server and flooding the log. currentDelay is unchanged on the failure path, so the last
                // good cadence is reused.
                var remaining = currentDelay - Stopwatch.GetElapsedTime(iterationStart);
                if (remaining > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(remaining, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }

        // Pure cadence policy (no I/O) so it can be unit tested without a database or the polling loop.
        internal static TimeSpan NextDelay(TimeSpan current, TimeSpan baseDelay, TimeSpan maxDelay, long maxObservedLength)
        {
            if (maxObservedLength > 0)
            {
                return baseDelay; // work present -> snap back to full speed
            }

            var doubled = TimeSpan.FromTicks(current.Ticks * 2);
            if (doubled < baseDelay)
            {
                doubled = baseDelay;
            }

            return doubled > maxDelay ? maxDelay : doubled;
        }

        void UpdateQueueLengthStore()
        {
            var nowTicks = DateTime.UtcNow.Ticks;

            foreach (var tableNamePair in tableNames)
            {
                Store(
                    [
                        new QueueLengthEntry
                        {
                            DateTicks = nowTicks,
                            Value = tableSizes.GetValueOrDefault(tableNamePair.Value, 0)
                        }
                    ],
                    tableNamePair.Key);
            }
        }

        async Task QueryTableSizes(CancellationToken cancellationToken)
        {
            // sys.partitions is per-database, so group the tracked tables by catalog and issue one
            // bulk query per distinct catalog. In the common single-catalog setup this is ONE query
            // per poll for the whole system, regardless of how many endpoints are monitored.
            var byCatalog = tableSizes.Keys.GroupBy(t => t.UnquotedCatalog);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var catalogGroup in byCatalog)
            {
                await QueryCatalog(connection, catalogGroup.Key, catalogGroup.ToArray(), cancellationToken);
            }
        }

        async Task QueryCatalog(SqlConnection connection, string catalog, SqlTable[] tables, CancellationToken cancellationToken)
        {
            var query = SqlTable.BuildBulkLengthQuery(catalog, tables);

            // (schema, name) -> length, for matching the result rows back to the tracked tables.
            var results = new Dictionary<(string, string), int>(SchemaNameComparer);

            await using (var command = new SqlCommand(query, connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var schema = reader.GetString(0);
                    var name = reader.GetString(1);
                    var length = reader.GetInt64(2); // SUM(p.rows) is bigint

                    results[(schema, name)] = length > int.MaxValue ? int.MaxValue : (int)length;
                }
            }

            foreach (var table in tables)
            {
                if (results.TryGetValue((table.UnquotedSchema, table.UnquotedName), out var length))
                {
                    // Indexer rather than TryUpdate: record the freshly read length unconditionally. A
                    // concurrent TrackEndpointInputQueue that removed this key could be resurrected here, but
                    // there is no untrack path today so the entry would persist regardless — kept simple.
                    tableSizes[table] = length;
                }
                else
                {
                    // No catalog row for this table -> the queue table does not (yet) exist. Record 0 rather
                    // than leaving a stale value: maxObservedLength (the back-off trigger) is a Max over
                    // tableSizes, so a lingering non-zero here would pin the cadence at the base interval and
                    // permanently defeat the adaptive back-off once a tracked queue is dropped.
                    tableSizes[table] = 0;
                    logger.LogWarning("Table {TableName} does not exist", table);
                }
            }
        }

        static readonly IEqualityComparer<(string, string)> SchemaNameComparer =
            new SchemaNameEqualityComparer();

        sealed class SchemaNameEqualityComparer : IEqualityComparer<(string, string)>
        {
            public bool Equals((string, string) x, (string, string) y) =>
                StringComparer.OrdinalIgnoreCase.Equals(x.Item1, y.Item1) &&
                StringComparer.OrdinalIgnoreCase.Equals(x.Item2, y.Item2);

            public int GetHashCode((string, string) obj) =>
                HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
        }

        readonly ConcurrentDictionary<EndpointToQueueMapping, SqlTable> tableNames = new ConcurrentDictionary<EndpointToQueueMapping, SqlTable>();
        readonly ConcurrentDictionary<SqlTable, int> tableSizes = new ConcurrentDictionary<SqlTable, int>();

        readonly string connectionString;
        readonly string defaultSchema;
        readonly TimeSpan baseDelay;
        readonly TimeSpan maxDelay;
        TimeSpan currentDelay;

        readonly ILogger<QueueLengthProvider> logger;

        // Base interval matches the finest monitoring bucket (1-minute history / 60 = 1s per point). With the
        // concurrent pacing above this yields ~one sample per bucket, so the old 200ms oversampling workaround
        // for #4556 is no longer needed.
        static readonly TimeSpan DefaultQueryDelayInterval = TimeSpan.FromSeconds(1);
        static readonly TimeSpan DefaultQueryMaxDelayInterval = TimeSpan.FromSeconds(10);
    }
}
