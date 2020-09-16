namespace Particular.ServiceControl.Licensing
{
    using System;
    using NServiceBus.Logging;
    using Particular.Licensing;

    class ActiveLicense
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
            var sources = new LicenseSource[]
            {
                new LicenseSourceFilePath(LicenseFileLocationResolver.GetPathFor(Environment.SpecialFolder.CommonApplicationData))
            };

            var result = Particular.Licensing.ActiveLicense.Find("ServiceControl", sources);

            IsValid = !result.License.HasExpired();

            if (!IsValid)
            {
                foreach (var report in result.Report)
                {
                    Logger.Info(report);
                }

                Logger.Warn("License has expired");
            }

            Details = result.License;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ActiveLicense));
    }
}