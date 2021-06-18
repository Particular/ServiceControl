namespace ServiceControl.Monitoring.Licensing
{
    using NServiceBus.Logging;
    using Particular.Licensing;

    public class LicenseManager
    {
        readonly Settings settings;
        internal License Details { get; set; }
        internal bool IsValid { get; set; }

        public LicenseManager(Settings settings) => this.settings = settings;

        public void Refresh()
        {
            Logger.Debug("Checking License Status");

            var sources = LicenseSource.GetStandardLicenseSources();
            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                sources.Add(new LicenseSourceUserProvided(settings.LicenseFileText));
            }
            var result = ActiveLicense.Find("ServiceControl", sources.ToArray());

            if (result.License.HasExpired())
            {
                foreach (var report in result.Report)
                {
                    Logger.Info(report);
                }

                Logger.Warn("License has expired");
            }

            IsValid = !result.License.HasExpired();
            Details = result.License;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ActiveLicense));
    }
}