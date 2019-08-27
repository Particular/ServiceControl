using System;

namespace ServiceControl.Monitoring.Licensing
{
    using System.IO;
    using NServiceBus.Logging;
    using Particular.Licensing;

    public class LicenseManager
    {
        internal static License Details { get; set; }
        internal static bool IsValid { get; set; }

        public void Refresh()
        {
            Logger.Debug("Checking License Status");
            var sources = LicenseSource.GetStandardLicenseSources();
            sources.Add(new LicenseSourceFilePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "License", "License.xml")));
            sources.Add(new LicenseSourceFilePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParticularPlatformLicense.xml")));
            var result = ActiveLicense.Find("ServiceControl", sources.ToArray());

            if (result.HasExpired)
            {
                foreach (var report in result.Report)
                {
                    Logger.Info(report);
                }
                Logger.Warn("License has expired");
            }

            IsValid = !result.HasExpired;
            Details = result.License;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ActiveLicense));
    }
    
}
