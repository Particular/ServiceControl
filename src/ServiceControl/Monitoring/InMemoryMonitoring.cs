namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;

    class InMemoryMonitoring : Feature
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
        }

        class MonitorEndpointInstances : FeatureStartupTask
        {
            private readonly EndpointInstanceMonitoring monitor;
            private readonly TimeKeeper timeKeeper;
            private readonly MonitoringDataPersister persistence;
            private Timer timer;

            public MonitorEndpointInstances(EndpointInstanceMonitoring monitor, TimeKeeper timeKeeper, MonitoringDataPersister persistence)
            {
                this.monitor = monitor;
                this.timeKeeper = timeKeeper;
                this.persistence = persistence;
            }

            public TimeSpan GracePeriod { get; set; }

            protected override async Task OnStart(IMessageSession session)
            {
                await persistence.WarmupMonitoringFromPersistence();
                timer = timeKeeper.New(CheckEndpoints, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            }

            private async Task CheckEndpoints()
            {
                try
                {
                    var inactivityThreshold = DateTime.UtcNow - GracePeriod;
                    log.Debug($"Monitoring Endpoint Instances. Inactivity Threshold = {inactivityThreshold}");
                    await monitor.CheckEndpoints(inactivityThreshold)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    log.Error("Exception occurred when monitoring endpoint instances", exception);
                }
            }

            protected override Task OnStop(IMessageSession session)
            {
                timeKeeper.Release(timer);
                return Task.FromResult(0);
            }
        }

        private static ILog log = LogManager.GetLogger<MonitorEndpointInstances>();
    }
}