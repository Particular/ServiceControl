namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;

    class HeartbeatMonitoringHostedService : IHostedService
    {
        public HeartbeatMonitoringHostedService(EndpointInstanceMonitoring monitor, IMonitoringDataStore persistence, IAsyncTimer scheduler, Settings settings)
        {
            this.monitor = monitor;
            this.persistence = persistence;
            this.scheduler = scheduler;
            gracePeriod = settings.HeartbeatGracePeriod;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await persistence.WarmupMonitoringFromPersistence(monitor).ConfigureAwait(false);
            timer = scheduler.Schedule(_ => CheckEndpoints(), TimeSpan.Zero, TimeSpan.FromSeconds(5), e => { log.Error("Exception occurred when monitoring endpoint instances", e); });
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

        async Task<TimerJobExecutionResult> CheckEndpoints()
        {
            var inactivityThreshold = DateTime.UtcNow - gracePeriod;
            if (log.IsDebugEnabled)
            {
                log.Debug($"Monitoring Endpoint Instances. Inactivity Threshold = {inactivityThreshold}");
            }

            await monitor.CheckEndpoints(inactivityThreshold).ConfigureAwait(false);
            return TimerJobExecutionResult.ScheduleNextExecution;
        }

        EndpointInstanceMonitoring monitor;
        IMonitoringDataStore persistence;
        IAsyncTimer scheduler;
        TimerJob timer;
        TimeSpan gracePeriod;

        static ILog log = LogManager.GetLogger<HeartbeatMonitoringHostedService>();
    }
}