namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.IO;
    using NServiceBus.Logging;
    using Particular.Licensing;

    public class ActiveLicense
    {
        public ActiveLicense()
        {
            Refresh();
        }

        public bool IsValid { get; set; }

        internal License Details { get; set; }

        public void Refresh()
        {
            Logger.Debug("Refreshing ActiveLicense");
            var sources = LicenseSource.GetStandardLicenseSources();
            sources.Add(new LicenseSourceFilePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "License", "License.xml")));
            sources.Add(new LicenseSourceFilePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParticularPlatformLicense.xml")));
            var result = Particular.Licensing.ActiveLicense.Find("ServiceControl", sources.ToArray());

            if (result.HasExpired)
            {
                foreach (var report in result.Report)
                {
                    Logger.Info(report);
                }

                Logger.Warn("License has expired");
            }

            Details = result.License;
            IsValid = !result.HasExpired;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ActiveLicense));
    }
}