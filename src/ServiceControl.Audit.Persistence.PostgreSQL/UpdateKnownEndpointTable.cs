namespace ServiceControl.Audit.Persistence.PostgreSQL;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

class UpdateKnownEndpointTable(
    ILogger<RetentionCleanupService> logger,
        PostgreSQLConnectionFactory connectionFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation($"{nameof(UpdateKnownEndpointTable)} started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

                await UpdateTable(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation($"{nameof(UpdateKnownEndpointTable)} stopped.");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during update known_endpoints table.");
            }
        }
    }

    async Task UpdateTable(CancellationToken stoppingToken)
    {
        await using var conn = await connectionFactory.OpenConnection(stoppingToken);

        var sql = @"
            DO $$
            BEGIN
                IF pg_try_advisory_xact_lock(hashtext('known_endpoints_sync')) THEN
                    INSERT INTO known_endpoints (id, name, host_id, host, last_seen)
                    SELECT DISTINCT ON (endpoint_id) endpoint_id, name, host_id, host, last_seen
                    FROM known_endpoints_insert
                    ORDER BY endpoint_id, last_seen DESC
                    ON CONFLICT (id) DO UPDATE SET
                        last_seen = GREATEST(known_endpoints.last_seen, EXCLUDED.last_seen);
                    
                    DELETE FROM known_endpoints_insert;
                END IF;
            END $$;
        ";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(stoppingToken);
    }
}
