namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.BackgroundTasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;

    class HeartbeatMonitoringHostedService : IHostedService
    {
        public HeartbeatMonitoringHostedService(IEndpointInstanceMonitoring monitor, IMonitoringDataStore persistence, IAsyncTimer scheduler, Settings settings, ILogger<HeartbeatMonitoringHostedService> logger)
        {
            this.monitor = monitor;
            this.persistence = persistence;
            this.scheduler = scheduler;
            this.logger = logger;
            gracePeriod = settings.HeartbeatGracePeriod;
        }
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await persistence.WarmupMonitoringFromPersistence(monitor, cancellationToken);
            timer = scheduler.Schedule(CheckEndpoints, TimeSpan.Zero, TimeSpan.FromSeconds(5), e => logger.LogError(e, "Exception occurred when monitoring endpoint instances"));
        }

        public Task StopAsync(CancellationToken cancellationToken = default) => timer.Stop(cancellationToken);

        async Task<TimerJobExecutionResult> CheckEndpoints(CancellationToken cancellationToken)
        {
            var inactivityThreshold = DateTime.UtcNow - gracePeriod;

            logger.LogDebug("Monitoring Endpoint Instances. Inactivity Threshold = {InactivityThreshold}", inactivityThreshold);

            await monitor.CheckEndpoints(inactivityThreshold, cancellationToken);
            return TimerJobExecutionResult.ScheduleNextExecution;
        }

        IEndpointInstanceMonitoring monitor;
        IMonitoringDataStore persistence;
        IAsyncTimer scheduler;
        TimerJob timer;
        TimeSpan gracePeriod;

        readonly ILogger<HeartbeatMonitoringHostedService> logger;
    }
}