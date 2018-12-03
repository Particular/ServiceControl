namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;

    class LicenseCheckFeature : Feature
    {
        public LicenseCheckFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ActiveLicense>(DependencyLifecycle.SingleInstance);
            context.RegisterStartupTask(b => b.Build<LicenseCheckFeatureStartup>());
        }
    }

    class LicenseCheckFeatureStartup : FeatureStartupTask
    {
        public LicenseCheckFeatureStartup(ActiveLicense activeLicense)
        {
            this.activeLicense = activeLicense;
        }

        protected override Task OnStart(IMessageSession session)
        {
            var due = TimeSpan.FromHours(8);
            timer = new AsyncTimer(_ =>
            {
                activeLicense.Refresh();
                return Task.FromResult(TimerJobExecutionResult.ScheduleNextExecution);
            }, due, due, ex =>
            {
                log.Error("Unhandled error while refreshing the license.", ex);
            });
            return Task.FromResult(0);
        }

        protected override Task OnStop(IMessageSession session)
        {
            return timer.Stop();
        }

        static ILog log = LogManager.GetLogger<LicenseCheckFeature>();
        ActiveLicense activeLicense;
        AsyncTimer timer;
    }
}