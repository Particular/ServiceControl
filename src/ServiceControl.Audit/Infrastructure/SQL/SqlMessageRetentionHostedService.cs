namespace ServiceControl.Monitoring
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Audit.Infrastructure;
    using Audit.Infrastructure.Settings;
    using Audit.Infrastructure.SQL;
    using Dapper;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;

    class SqlMessageRetentionHostedService : IHostedService
    {
        public SqlMessageRetentionHostedService(string connectionString, Settings settings)
        {
            this.connectionString = connectionString;
            retentionPeriod = settings.AuditRetentionPeriod;
            batchSize = settings.ExpirationProcessBatchSize;
            expirationIntervalInSeconds = settings.ExpirationProcessTimerInSeconds;
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            timer = new AsyncTimer(
                token => Cleanup(),
                TimeSpan.Zero,
                TimeSpan.FromSeconds(expirationIntervalInSeconds),
                e => { log.Error("Error when trying to find expired documents", e); });

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await timer.Stop().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //NOOP
            }
        }

        async Task<TimerJobExecutionResult> Cleanup()
        {
            if (log.IsDebugEnabled)
            {
                log.Debug($"Staring retention query. Retention Period = {retentionPeriod}");
            }

            Console.WriteLine($"Staring retention query. Retention Period = {retentionPeriod}");
            Console.WriteLine($"Staring retention query. Processed At = {DateTime.Now.Subtract(retentionPeriod)}");

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.ExecuteAsync(SqlConstants.RemoveOutdatedMessages,
                    new
                    {
                        ProcessedAt = DateTime.Now.Subtract(retentionPeriod),
                        TotalRows = batchSize
                    }).ConfigureAwait(false);
            }

            return TimerJobExecutionResult.ScheduleNextExecution;
        }

        AsyncTimer timer;
        TimeSpan retentionPeriod;
        string connectionString;
        int batchSize;
        int expirationIntervalInSeconds;

        static ILog log = LogManager.GetLogger<SqlMessageRetentionHostedService>();
    }
}