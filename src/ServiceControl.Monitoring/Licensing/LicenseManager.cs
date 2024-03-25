namespace ServiceControl.Monitoring.Licensing
{
    using LicenseManagement;
    using NServiceBus.Logging;

    public class LicenseManager
    {
        internal LicenseDetails Details { get; set; }
        internal bool IsValid { get; set; }

        public void Refresh()
        {
            Logger.Debug("Checking License Status");

            var detectedLicense = ServiceControl.LicenseManagement.LicenseManager.FindLicense();

            IsValid = !detectedLicense.Details.HasLicenseExpired();

            Details = detectedLicense.Details;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(LicenseManager));
    }
}