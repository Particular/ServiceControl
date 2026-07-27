namespace ServiceControl.Persistence.EFCore.SqlServer.CustomChecks
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DbContexts;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NServiceBus.CustomChecks;
    using ServiceControl.Infrastructure;

    class CheckFreeDiskSpace(IServiceScopeFactory serviceScopeFactory, SqlServerPersisterSettings settings, ILogger<CheckFreeDiskSpace> logger)
        : CustomCheck("ServiceControl database", "Storage space", TimeSpan.FromMinutes(5))
    {
        public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            logger.LogDebug("Check ServiceControl data drive space remaining custom check starting. Threshold {PercentageThreshold:P0}", percentageThreshold);

            var databaseSpace = await TryGetDatabaseSpace(cancellationToken);
            if (databaseSpace == null)
            {
                logger.LogWarning("Unable to retrieve database space information.");
                return CheckResult.Pass;
            }

            var (totalSpaceMb, availableFreeSpaceMb) = databaseSpace.Value;
            var percentRemaining = totalSpaceMb == 0 ? 0 : (decimal)availableFreeSpaceMb / totalSpaceMb;

            logger.LogDebug("Free space: {FreeSpaceTotalBytesFree:N0}MB | Total: {FreeSpaceTotalBytesAvailable:N0}M | Remaining {PercentRemaining:P1}%", availableFreeSpaceMb, totalSpaceMb, percentRemaining);

            return percentRemaining > percentageThreshold
                ? CheckResult.Pass
                : CheckResult.Failed($"{percentRemaining:P0} disk space remaining on SQLServer database.");
        }

        public static void Validate(SqlServerPersisterSettings settings)
        {
            var logger = LoggerUtil.CreateStaticLogger<CheckFreeDiskSpace>();
            var threshold = settings.DataSpaceRemainingThreshold;

            if (threshold < 0)
            {
                logger.LogCritical("{ConfigKey} is invalid, minimum value is 0", PersistenceConfiguration.DataSpaceRemainingThresholdKey);
                throw new Exception($"{PersistenceConfiguration.DataSpaceRemainingThresholdKey} is invalid, minimum value is 0.");
            }

            if (threshold > 100)
            {
                logger.LogCritical("{ConfigKey} is invalid, maximum value is 100", PersistenceConfiguration.DataSpaceRemainingThresholdKey);
                throw new Exception($"{PersistenceConfiguration.DataSpaceRemainingThresholdKey} is invalid, maximum value is 100.");
            }
        }

        internal async Task<(long totalSpaceMb, long availableFreeSpaceMb)?> TryGetDatabaseSpace(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
                return await TryGetDatabaseSpace(dbContext, cancellationToken);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static async Task<(long totalSpaceMb, long availableFreeSpaceMb)?> TryGetDatabaseSpace(DbContext dbContext, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = dbContext.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                await using var command = connection.CreateCommand();
                command.CommandText = """
                                      SELECT
                                          SUM(size) * 8.0 / 1024 AS CurrentSizeMB,
                                          SUM(CASE WHEN max_size = -1 THEN CAST(size AS BIGINT) ELSE CAST(max_size AS BIGINT) END) * 8.0 / 1024 AS MaxSizeMB
                                      FROM sys.database_files
                                      WHERE type_desc = 'ROWS';
                                      """;

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0) || reader.IsDBNull(1))
                {
                    return null;
                }

                var currentSizeMb = reader.GetInt64(0);
                var maxSizeMb = reader.GetInt64(1);
                var availableMb = Math.Max(0L, maxSizeMb - currentSizeMb);

                return (maxSizeMb, availableMb);
            }
            catch (Exception)
            {
                return null;
            }
        }

        readonly decimal percentageThreshold = settings.DataSpaceRemainingThreshold / 100m;
    }
}