namespace ServiceControl.Transports.SqlServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using NServiceBus.Logging;

    class QueueLengthProvider : AbstractQueueLengthProvider
    {
        public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store) : base(settings, store)
        {
            connectionString = ConnectionString
                .RemoveCustomConnectionStringParts(out var customSchema, out _);

            defaultSchema = customSchema ?? "dbo";
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
                try
                {
                    await Task.Delay(QueryDelayInterval, stoppingToken);

                    await QueryTableSizes(stoppingToken);

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
            var chunks = tableSizes
                .Select((i, index) => new
                {
                    i,
                    index
                })
                .GroupBy(p => p.index / QueryChunkSize)
                .Select(grp => grp.Select(g => g.i).ToArray())
                .ToList();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            foreach (var chunk in chunks)
            {
                await UpdateChunk(connection, chunk, cancellationToken);
            }
        }

        async Task UpdateChunk(SqlConnection connection, KeyValuePair<SqlTable, int>[] chunk, CancellationToken cancellationToken)
        {
            var query = string.Join(Environment.NewLine, chunk.Select(c => BuildQueueLengthQuery(c.Key)).ToArray());

            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
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
                return $"""
                        IF (EXISTS (SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{t.UnquotedSchema}' AND TABLE_NAME = '{t.UnquotedName}'))
                          SELECT isnull(cast(max([RowVersion]) - min([RowVersion]) + 1 AS int), 0) FROM {t.QuotedSchema}.{t.QuotedName} WITH (nolock)
                        ELSE
                          SELECT -1;
                        """;
            }

            return $"""
                    IF (EXISTS (SELECT TABLE_NAME FROM {t.QuotedCatalog}.INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{t.UnquotedSchema}' AND TABLE_NAME = '{t.UnquotedName}'))
                      SELECT isnull(cast(max([RowVersion]) - min([RowVersion]) + 1 AS int), 0) FROM {t.QuotedCatalog}.{t.QuotedSchema}.{t.QuotedName} WITH (nolock)
                    ELSE
                      SELECT -1;
                    """;
        }

        readonly ConcurrentDictionary<EndpointToQueueMapping, SqlTable> tableNames = new ConcurrentDictionary<EndpointToQueueMapping, SqlTable>();
        readonly ConcurrentDictionary<SqlTable, int> tableSizes = new ConcurrentDictionary<SqlTable, int>();

        readonly string connectionString;
        readonly string defaultSchema;

        static readonly ILog Logger = LogManager.GetLogger<QueueLengthProvider>();

        static readonly TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

        const int QueryChunkSize = 10;
    }
}
