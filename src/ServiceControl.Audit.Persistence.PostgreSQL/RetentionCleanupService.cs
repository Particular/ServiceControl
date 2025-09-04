namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

class RetentionCleanupService(
        ILogger<RetentionCleanupService> logger,
        DatabaseConfiguration config,
        PostgreSQLConnectionFactory connectionFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation($"{nameof(RetentionCleanupService)} started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(config.ExpirationProcessTimerInSeconds), stoppingToken);

                await CleanupOldMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during cleanup task.");
            }
        }

        logger.LogInformation($"{nameof(RetentionCleanupService)} stopped.");
    }

    async Task CleanupOldMessagesAsync(CancellationToken cancellationToken)
    {
        await using var conn = await connectionFactory.OpenConnection(cancellationToken);

        var cutoffDate = DateTime.UtcNow - config.AuditRetentionPeriod;

        // Cleanup processed messages
        await CleanupTable("processed_messages", "created_at", cutoffDate, conn, cancellationToken);

        // Cleanup saga snapshots
        await CleanupTable("saga_snapshots", "updated_at", cutoffDate, conn, cancellationToken);

        // Cleanup known endpoints
        await CleanupTable("known_endpoints", "updated_at", cutoffDate, conn, cancellationToken);
    }

    async Task CleanupTable(string tableName, string dateColumn, DateTime cutoffDate, NpgsqlConnection conn, CancellationToken cancellationToken)
    {
        var totalDeleted = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Delete in batches
            var sql = $@"
                DELETE FROM {tableName}
                WHERE ctid IN (
                    SELECT ctid FROM {tableName}
                    WHERE {dateColumn} < @cutoff
                    LIMIT 1000
                );";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("cutoff", cutoffDate);

            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            totalDeleted += rows;

            if (rows < 1000)
            {
                break; // no more rows to delete in this run
            }

            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
        }

        if (totalDeleted > 0)
        {
            logger.LogInformation("Deleted {Count} old records from {Table} older than {Cutoff}", totalDeleted, tableName, cutoffDate);
        }
    }
}
