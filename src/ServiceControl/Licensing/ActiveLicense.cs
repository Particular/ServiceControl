namespace Particular.ServiceControl.Licensing
{
    using NServiceBus.Logging;
    using Particular.Licensing;
    using ServiceBus.Management.Infrastructure.Settings;

    class ActiveLicense
    {
        readonly Settings settings;

        public ActiveLicense(Settings settings)
        {
            this.settings = settings;
            Refresh();
        }

        public bool IsValid { get; set; }

        internal License Details { get; set; }

        public void Refresh()
        {
            Logger.Debug("Refreshing ActiveLicense");

            var sources = LicenseSource.GetStandardLicenseSources();
            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                sources.Add(new LicenseSourceUserProvided(settings.LicenseFileText));
            }

            var result = Particular.Licensing.ActiveLicense.Find("ServiceControl", sources.ToArray());

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