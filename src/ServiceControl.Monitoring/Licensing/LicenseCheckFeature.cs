namespace ServiceControl.Monitoring.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;

    class LicenseCheckFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<LicenseManager>(DependencyLifecycle.SingleInstance);
            context.RegisterStartupTask(b => b.Build<LicenseCheckFeatureStartup>());
        }
    }

    class LicenseCheckFeatureStartup : FeatureStartupTask, IDisposable
    {
        public LicenseCheckFeatureStartup(LicenseManager licenseManager)
        {
            this.licenseManager = licenseManager;
        }

        public void Dispose()
        {
            checklicenseTimer?.Dispose();
        }

        protected override Task OnStart(IMessageSession session)
        {
            return Task.Run(() => checklicenseTimer = new Timer(objectstate => { licenseManager.Refresh(); }, null, TimeSpan.Zero, TimeSpan.FromHours(8)));
        }

        protected override Task OnStop(IMessageSession session)
        {
            return Task.FromResult(0);
        }

        Timer checklicenseTimer;
        LicenseManager licenseManager;
    }
}