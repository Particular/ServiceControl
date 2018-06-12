namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ServiceControl.Infrastructure;
    using NServiceBus;
    using NServiceBus.Features;

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
        private ActiveLicense activeLicense;
        private readonly TimeKeeper timeKeeper;
        private Timer timer;

        public LicenseCheckFeatureStartup(ActiveLicense activeLicense, TimeKeeper timeKeeper)
        {
            this.timeKeeper = timeKeeper;
            this.activeLicense = activeLicense;
        }

        protected override Task OnStart(IMessageSession session)
        {
            var due = TimeSpan.FromHours(8);
            timer = timeKeeper.New(() =>
            {
                activeLicense.Refresh();
            }, due, due);
            return Task.FromResult(0);
        }

        protected override Task OnStop(IMessageSession session)
        {
            timeKeeper.Release(timer);
            return Task.FromResult(0);
        }
    }
}
