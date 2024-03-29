﻿namespace ServiceControl.Transports.SqlServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using NServiceBus.Logging;

    class QueueLengthProvider : IProvideQueueLength
    {
        public void Initialize(string connectionString, Action<QueueLengthEntry[], EndpointToQueueMapping> store)
        {
            this.connectionString = connectionString
                .RemoveCustomConnectionStringParts(out var customSchema, out _);

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
                        await Task.Delay(QueryDelayInterval, token);

                        await QueryTableSizes(token);

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
                    new[]
                    {
                        new QueueLengthEntry
                        {
                            DateTicks = nowTicks,
                            Value = tableSizes.TryGetValue(tableNamePair.Value, out var size) ? size : 0
                        }
                    },
                    tableNamePair.Key);
            }
        }

        public Task Stop()
        {
            stop.Cancel();

            return poller;
        }

        async Task QueryTableSizes(CancellationToken cancellationToken)
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
                await connection.OpenAsync(cancellationToken);

                foreach (var chunk in chunks)
                {
                    await UpdateChunk(connection, chunk, cancellationToken);
                }
            }
        }

        async Task UpdateChunk(SqlConnection connection, KeyValuePair<SqlTable, int>[] chunk, CancellationToken cancellationToken)
        {
            var query = string.Join(Environment.NewLine, chunk.Select(c => BuildQueueLengthQuery(c.Key)).ToArray());

            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    foreach (var chunkPair in chunk)
                    {
                        await reader.ReadAsync(cancellationToken);

                        var queueLength = reader.GetInt32(0);

                        if (queueLength == -1)
                        {
                            Logger.Warn($"Table {chunkPair.Key} does not exist.");
                        }
                        else
                        {
                            tableSizes.TryUpdate(chunkPair.Key, queueLength, chunkPair.Value);
                        }

                        await reader.NextResultAsync(cancellationToken);
                    }
                }
            }
        }

        static string BuildQueueLengthQuery(SqlTable t)
        {
            //HINT: The query approximates queue length value based on max and min
            //      of RowVersion IDENTITY(1,1) column. There are couple of scenarios
            //      that might lead to the approximation being off. More details here:
            //      https://docs.microsoft.com/en-us/sql/t-sql/statements/create-table-transact-sql-identity-property?view=sql-server-ver15#remarks
            //
            //      Min and Max values return NULL when no rows are found.
            if (t.QuotedCatalog == null)
            {
                return $@"IF (EXISTS (SELECT *  FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{t.UnquotedSchema}' AND  TABLE_NAME = '{t.UnquotedName}'))
                            SELECT isnull(cast(max([RowVersion]) - min([RowVersion]) + 1 AS int), 0) FROM {t.QuotedSchema}.{t.QuotedName} WITH (nolock)
                          ELSE
                            SELECT -1;";
            }

            return $@"IF (EXISTS (SELECT *  FROM {t.QuotedCatalog}.INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{t.UnquotedSchema}' AND  TABLE_NAME = '{t.UnquotedName}'))
                        SELECT isnull(cast(max([RowVersion]) - min([RowVersion]) + 1 AS int), 0) FROM {t.QuotedCatalog}.{t.QuotedSchema}.{t.QuotedName} WITH (nolock)
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
