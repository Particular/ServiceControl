namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading;
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
            RegisterStartupTask<MonitorEndpointInstances>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");
            context.Container.ConfigureComponent<MonitorEndpointInstances>(DependencyLifecycle.SingleInstance)
                .ConfigureProperty(p => p.GracePeriod, settings.HeartbeatGracePeriod);
            context.Container.ConfigureComponent<EndpointInstanceMonitoring>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<MonitoringDataPersister>(DependencyLifecycle.SingleInstance);
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

            protected override void OnStart()
            {
                persistence.WarmupMonitoringFromPersistence();
                timer = timeKeeper.NewTimer(CheckEndpoints, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            }

            private bool CheckEndpoints()
            {
                try
                {
                    var inactivityThreshold = DateTime.UtcNow - GracePeriod;
                    log.Debug($"Monitoring Endpoint Instances. Inactivity Threshold = {inactivityThreshold}");
                    monitor.CheckEndpoints(inactivityThreshold);
                }
                catch (Exception exception)
                {
                    log.Error("Exception occurred when monitoring endpoint instances", exception);
                }
                return true;
            }

            protected override void OnStop()
            {
                timeKeeper.Release(timer);
            }
        }

        private static ILog log = LogManager.GetLogger<MonitorEndpointInstances>();
    }
}