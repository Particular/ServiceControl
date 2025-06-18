namespace ServiceControl.Transports.PostgreSql;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

class QueueLengthProvider : AbstractQueueLengthProvider
{
    public QueueLengthProvider(TransportSettings settings, Action<QueueLengthEntry[], EndpointToQueueMapping> store, ILogger<QueueLengthProvider> logger) : base(settings, store)
    {
        connectionString = ConnectionString
            .RemoveCustomConnectionStringParts(out var customSchema, out _);

        defaultSchema = customSchema ?? "public";
        this.logger = logger;
    }
    public override void TrackEndpointInputQueue(EndpointToQueueMapping queueToTrack)
    {
        var parsedAddress = QueueAddress.Parse(queueToTrack.InputQueue);

        var sqlTable = new PostgreSqlTable(parsedAddress.Table, parsedAddress.Schema ?? defaultSchema);

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
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // no-op
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error querying SQL queue sizes.");
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

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var chunk in chunks)
        {
            await UpdateChunk(connection, chunk, cancellationToken);
        }
    }

    async Task UpdateChunk(NpgsqlConnection connection, KeyValuePair<PostgreSqlTable, int>[] chunk, CancellationToken cancellationToken)
    {
        var query = string.Join(Environment.NewLine, chunk.Select(c => c.Key.LengthQuery));

        await using var command = new NpgsqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        foreach (var chunkPair in chunk)
        {
            await reader.ReadAsync(cancellationToken);

            var queueLength = reader.GetInt32(0);

            if (queueLength == -1)
            {
                logger.LogWarning("Table {TableName} does not exist.", chunkPair.Key);
            }
            else
            {
                tableSizes.TryUpdate(chunkPair.Key, queueLength, chunkPair.Value);
            }

            await reader.NextResultAsync(cancellationToken);
        }
    }

    readonly ConcurrentDictionary<EndpointToQueueMapping, PostgreSqlTable> tableNames = new ConcurrentDictionary<EndpointToQueueMapping, PostgreSqlTable>();
    readonly ConcurrentDictionary<PostgreSqlTable, int> tableSizes = new ConcurrentDictionary<PostgreSqlTable, int>();

    readonly string connectionString;
    readonly string defaultSchema;

    readonly ILogger<QueueLengthProvider> logger;

    static readonly TimeSpan QueryDelayInterval = TimeSpan.FromMilliseconds(200);

    const int QueryChunkSize = 10;
}