namespace ServiceControl.Transports.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Collections.Concurrent;
    using System.Data.SqlClient;
    using System.Threading;
    using NServiceBus.Logging;

    class QueueLengthProvider: IProvideQueueLength
    {
        public void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
            this.connectionString = connectionString.RemoveCustomSchemaPart(out var customSchema);

            defaultSchema = customSchema ?? "dbo";

            this.store = store;
        }

        public void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
        {
            var sqlTable = SqlTable.Parse(queueToTrack.InputQueue, defaultSchema);

            tableNames.AddOrUpdate(queueToTrack, _ => sqlTable, (_, currentSqlTable) =>
            {
                if (currentSqlTable.Equals(sqlTable) == false)
                {
                    tableSizes.TryRemove(currentSqlTable, out var _);
                }

                return sqlTable;
            });

            tableSizes.TryAdd(sqlTable, 0);
        }

        public Task Start()
        {
            stop = new CancellationTokenSource();

            poller = Task.Run(async () =>
            {
                var token = stop.Token;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(QueryDelayInterval, token).ConfigureAwait(false);

                        await QueryTableSizes(token).ConfigureAwait(false);

                        UpdateQueueLengthStore();
                    }
                    catch (OperationCanceledException)
                    {
                        // no-op
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Error querying sql queue sizes.", e);
                    }
                }
            });

            return Task.CompletedTask;
        }

        void UpdateQueueLengthStore()
        {
            var nowTicks = DateTime.UtcNow.Ticks;

            foreach (var tableNamePair in tableNames)
            {
                store(
                    new[]{ new QueueLengthEntry
                    {
                        DateTicks = nowTicks,
                        Value = tableSizes.TryGetValue(tableNamePair.Value, out var size) ? size : 0
                    }},
                    tableNamePair.Key);
            }
        }

        public Task Stop()
        {
            stop.Cancel();

            return poller;
        }

        async Task QueryTableSizes(CancellationToken token)
        {
            var chunks = tableSizes
                .Select((i, index) => new
                {
                    i,
                    index
                })
                .GroupBy(p => p.index / QueryChunkSize)
                .Select(grp => grp.Select(g => g.i).ToArray())
                .ToList();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                foreach (var chunk in chunks)
                {
                    await UpdateChunk(connection, chunk, token).ConfigureAwait(false);
                }
            }
        }

        async Task UpdateChunk(SqlConnection connection, KeyValuePair<SqlTable, int>[] chunk, CancellationToken token)
        {
            var query = string.Join(Environment.NewLine, chunk.Select(c => BuildQueueLengthQuery(c.Key)).ToArray());

            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    foreach (var chunkPair in chunk)
                    {
                        await reader.ReadAsync(token).ConfigureAwait(false);

                        var queueLength = reader.GetInt32(0);

                        if (queueLength == -1)
                        {
                            Logger.Warn($"Table {chunkPair.Key} does not exist.");
                        }
                        else
                        {
                            tableSizes.TryUpdate(chunkPair.Key, queueLength, chunkPair.Value);
                        }

                        await reader.NextResultAsync(token).ConfigureAwait(false);
                    }
                }
            }
        }

        static string BuildQueueLengthQuery(SqlTable t)
        {
            if (t.QuotedCatalog == null)
            {
                return $@"IF (EXISTS (SELECT *  FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{t.UnquotedSchema}' AND  TABLE_NAME = '{t.UnquotedName}'))
                            SELECT count(*) FROM {t.QuotedSchema}.{t.QuotedName} WITH (nolock)
                          ELSE
                            SELECT -1;";
            }

            return $@"IF (EXISTS (SELECT *  FROM {t.QuotedCatalog}.INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{t.UnquotedSchema}' AND  TABLE_NAME = '{t.UnquotedName}'))
                        SELECT count(*) FROM {t.QuotedCatalog}.{t.QuotedSchema}.{t.QuotedName} WITH (nolock)
                      ELSE
                        SELECT -1;";
        }

        Action<QueueLengthEntry[], EndpointToQueueMapping> store;

        ConcurrentDictionary<EndpointToQueueMapping, SqlTable> tableNames = new ConcurrentDictionary<EndpointToQueueMapping, SqlTable>();
        ConcurrentDictionary<SqlTable, int> tableSizes = new ConcurrentDictionary<SqlTable, int>();

        string connectionString;
        string defaultSchema;

        CancellationTokenSource stop = new CancellationTokenSource();
        Task poller;

        static ILog Logger = LogManager.GetLogger<QueueLengthProvider>();

        static TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        const int QueryChunkSize = 10;
    }
}
