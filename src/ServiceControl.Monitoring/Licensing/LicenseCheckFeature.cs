namespace ServiceControl.Monitoring.Licensing
{
    using NServiceBus;
    using NServiceBus.Features;
    using System;
    using System.Threading.Tasks;
    using System.Threading;
    
    class LicenseCheckFeature : Feature
    {
        public LicenseCheckFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(new LicenseCheckFeatureStartup());
        }
    }

    class LicenseCheckFeatureStartup : FeatureStartupTask, IDisposable
    {
        Timer checklicenseTimer;
        LicenseManager licenseManager = new LicenseManager();

        protected override Task OnStart(IMessageSession session)
        {
            return Task.Run(() => checklicenseTimer = new Timer( objectstate => { licenseManager.Refresh(); }, null, TimeSpan.Zero, TimeSpan.FromHours(8)));
        }

        public void Dispose()
        {
            checklicenseTimer?.Dispose();
        }

        protected override Task OnStop(IMessageSession session)
        {
            return Task.FromResult(0);
        }
    }
}

