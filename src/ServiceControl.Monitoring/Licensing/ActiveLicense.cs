namespace ServiceControl.Monitoring.Licensing
{
    using ServiceControl.LicenseManagement;
    using NServiceBus.Logging;

    public class ActiveLicense
    {
        public ActiveLicense() => Refresh();

        public bool IsValid { get; set; }

        internal LicenseDetails Details { get; set; }

        public void Refresh()
        {
            Logger.Debug("Refreshing ActiveLicense");

            var detectedLicense = LicenseManager.FindLicense();

            IsValid = !detectedLicense.Details.HasLicenseExpired();

            Details = detectedLicense.Details;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ActiveLicense));
    }
}