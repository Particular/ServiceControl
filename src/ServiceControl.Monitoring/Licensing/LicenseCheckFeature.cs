namespace ServiceControl.Monitoring.Licensing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Features;

    class LicenseCheckFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Services.AddSingleton<LicenseManager>();
            context.RegisterStartupTask(b => b.GetRequiredService<LicenseCheckFeatureStartup>());
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

        protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => checklicenseTimer = new Timer(objectstate => { licenseManager.Refresh(); }, null, TimeSpan.Zero, TimeSpan.FromHours(8)));
        }

        protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        Timer checklicenseTimer;
        LicenseManager licenseManager;
    }
}