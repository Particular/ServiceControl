namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Threading;
    using global::ServiceControl.Infrastructure;
    using NServiceBus;
    using NServiceBus.Features;
    
    class LicenseCheckFeature : Feature
    {
        public LicenseCheckFeature()
        {
            EnableByDefault();
            RegisterStartupTask<LicenseCheckFeatureStartup>();
        }
        
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ActiveLicense>(DependencyLifecycle.SingleInstance);
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

        protected override void OnStart()
        {
            var due = TimeSpan.FromHours(8);
            timer = timeKeeper.New(activeLicense.Refresh, due, due);
        }

        protected override void OnStop()
        {
            timeKeeper.Release(timer);
        }
    }
}
