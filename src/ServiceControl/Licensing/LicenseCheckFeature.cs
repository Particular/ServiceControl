namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.Threading;
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
        ActiveLicense activeLicense;
        Timer timer;

        public LicenseCheckFeatureStartup(ActiveLicense activeLicense)
        {
            this.activeLicense = activeLicense;
        }

        protected override void OnStart()
        {
            timer = new Timer(Refresh, null, (int)TimeSpan.FromMinutes(1).TotalMilliseconds, -1);
        }

        protected override void OnStop()
        {
            using (var manualResetEvent = new ManualResetEvent(false))
            {
                timer.Dispose(manualResetEvent);
                manualResetEvent.WaitOne();
            }
        }

        void Refresh(object _)
        {
            activeLicense.Refresh();
            try
            {
                timer.Change((int)TimeSpan.FromMinutes(1).TotalMilliseconds, -1);
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
