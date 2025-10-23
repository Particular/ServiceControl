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
            gracePeriod = settings.ServiceControl.HeartbeatGracePeriod;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await persistence.WarmupMonitoringFromPersistence(monitor);
            timer = scheduler.Schedule(_ => CheckEndpoints(), TimeSpan.Zero, TimeSpan.FromSeconds(5), e => logger.LogError(e, "Exception occurred when monitoring endpoint instances"));
        }

        public Task StopAsync(CancellationToken cancellationToken) => timer.Stop(cancellationToken);

        async Task<TimerJobExecutionResult> CheckEndpoints()
        {
            var inactivityThreshold = DateTime.UtcNow - gracePeriod;

            logger.LogDebug("Monitoring Endpoint Instances. Inactivity Threshold = {InactivityThreshold}", inactivityThreshold);

            await monitor.CheckEndpoints(inactivityThreshold);
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