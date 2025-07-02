namespace ServiceControl.Monitoring.Licensing
{
    using ServiceControl.LicenseManagement;
    using Microsoft.Extensions.Logging;

    public class ActiveLicense
    {
        public ActiveLicense(ILogger<ActiveLicense> logger)
        {
            this.logger = logger;
            Refresh();
        }

        public bool IsValid { get; set; }

        internal LicenseDetails Details { get; set; }

        public void Refresh()
        {
            logger.LogDebug("Refreshing ActiveLicense");

            var detectedLicense = LicenseManager.FindLicense();

            IsValid = !detectedLicense.Details.HasLicenseExpired();

            Details = detectedLicense.Details;
        }

        readonly ILogger<ActiveLicense> logger;
    }
}