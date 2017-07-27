namespace Particular.ServiceControl.Licensing
{
    using System;
    using System.IO;
    using NServiceBus.Logging;
    using Particular.Licensing;

    public class ActiveLicense
    {
        public bool IsValid { get; set; }

        internal License Details { get; set; }

        public ActiveLicense()
        {
            Refresh();
        }

        public void Refresh()
        {
            Logger.Debug("Refreshing ActiveLicense");
            var result = Particular.Licensing.ActiveLicense.Find("ServiceControl",
               new LicenseSourceHKLMRegKey(),
               new LicenseSourceFilePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "License", "License.xml")),
               new LicenseSourceFilePath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ParticularPlatformLicense.xml")));

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