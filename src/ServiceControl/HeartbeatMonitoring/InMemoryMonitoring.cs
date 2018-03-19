namespace ServiceControl.Monitoring
{
    using NServiceBus;
    using NServiceBus.Features;
    using Particular.HealthMonitoring.Uptime;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;

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

            context.Container.ConfigureComponent(
                b => new UptimeMonitoring(settings.HeartbeatGracePeriod, b.Build<IDomainEvents>(), b.Build<ITimeKeeper>()), 
                DependencyLifecycle.SingleInstance
             );

            context.Container.ConfigureComponent<MonitoringDataPersister>(DependencyLifecycle.SingleInstance);
        }

        class MonitorEndpointInstances : FeatureStartupTask
        {
            MonitoringDataPersister persistence;
            UptimeMonitoring uptimeMonitoring;

            public MonitorEndpointInstances(MonitoringDataPersister persistence, UptimeMonitoring uptimeMonitoring)
            {
                this.persistence = persistence;
                this.uptimeMonitoring = uptimeMonitoring;
            }

            protected override void OnStart()
            {
                persistence.WarmupMonitoringFromPersistence();
                uptimeMonitoring.Start();
            }

            protected override void OnStop()
            {
                uptimeMonitoring.Stop();
            }
        }

    }
}