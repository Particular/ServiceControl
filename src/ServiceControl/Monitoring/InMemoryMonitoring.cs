namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.BackgroundTasks;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    static class HeartbeatsHostBuilderExtensions
    {
        public static IHostBuilder UseHeartbeatMonitoring(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(collection =>
            {
                collection.AddHostedService<HeartbeatMonitoringHostedService>();
                collection.AddSingleton<MonitoringDataStore>();
                collection.AddDomainEventHandler<MonitoringDataPersister>();
            });
            return hostBuilder;
        }
    }

    class HeartbeatMonitoringHostedService : IHostedService
    {
        public HeartbeatMonitoringHostedService(EndpointInstanceMonitoring monitor, MonitoringDataStore persistence, IAsyncTimer scheduler, Settings settings)
        {
            this.monitor = monitor;
            this.persistence = persistence;
            this.scheduler = scheduler;
            gracePeriod = settings.HeartbeatGracePeriod;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await persistence.WarmupMonitoringFromPersistence().ConfigureAwait(false);
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
        MonitoringDataStore persistence;
        IAsyncTimer scheduler;
        TimerJob timer;
        TimeSpan gracePeriod;

        static ILog log = LogManager.GetLogger<HeartbeatMonitoringHostedService>();
    }
}