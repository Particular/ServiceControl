namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Heartbeats.Monitoring;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    public class InMemoryMonitoring : Feature
    {
        public InMemoryMonitoring()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");

            context.RegisterStartupTask(b =>
            {
                var instances = b.Build<MonitorEndpointInstances>();
                instances.GracePeriod = settings.HeartbeatGracePeriod;
                return instances;
            });

            context.Container.ConfigureComponent<MessageFailedHandler>(DependencyLifecycle.SingleInstance);
        }

        static ILog log = LogManager.GetLogger<MonitorEndpointInstances>();


        class MonitorEndpointInstances : FeatureStartupTask
        {
            public MonitorEndpointInstances(EndpointInstanceMonitoring monitor, MonitoringDataPersister persistence)
            {
                this.monitor = monitor;
                this.persistence = persistence;
            }

            public TimeSpan GracePeriod { get; set; }

            protected override async Task OnStart(IMessageSession session)
            {
                await persistence.WarmupMonitoringFromPersistence().ConfigureAwait(false);
                timer = new AsyncTimer(_ => CheckEndpoints(), TimeSpan.Zero, TimeSpan.FromSeconds(5), e => { log.Error("Exception occurred when monitoring endpoint instances", e); });
            }

            async Task<TimerJobExecutionResult> CheckEndpoints()
            {
                var inactivityThreshold = DateTime.UtcNow - GracePeriod;
                if (log.IsDebugEnabled)
                {
                    log.Debug($"Monitoring Endpoint Instances. Inactivity Threshold = {inactivityThreshold}");
                }

                await monitor.CheckEndpoints(inactivityThreshold).ConfigureAwait(false);
                return TimerJobExecutionResult.ScheduleNextExecution;
            }

            protected override Task OnStop(IMessageSession session)
            {
                return timer.Stop();
            }

            EndpointInstanceMonitoring monitor;
            MonitoringDataPersister persistence;
            AsyncTimer timer;
        }
    }
}