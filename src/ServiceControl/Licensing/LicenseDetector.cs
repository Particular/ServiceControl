namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.IO;
    using NServiceBus;
    using NServiceBus.Logging;
    using Particular.Licensing;

    public class LicenseDetector : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            var activeLicense = DetermineActiveLicense();
            configuration.RegisterComponents(c => c.RegisterSingleton(activeLicense));
        }

        static ActiveLicense DetermineActiveLicense()
        {

            var result = Particular.Licensing.ActiveLicense.Find("ServiceControl",
                new LicenseSourceHKLMRegKey(),
                new LicenseSourceFilePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "License", "License.xml")),
                new LicenseSourceFilePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParticularPlatformLicense.xml")));

            foreach (var report in result.Report)
            {
                Logger.Info(report);
            }

            if (result.HasExpired)
            {
                Logger.Warn("License has expired");
            }

            //Return the class ServicePulse expects
            return new ActiveLicense
            {
                Details = result.License,
                IsValid = !result.HasExpired,
                HasExpired = result.HasExpired
            };
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(LicenseDetector));
    }
}