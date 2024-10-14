namespace Particular.ServiceControl.Licensing
{
    using System.Threading.Tasks;
    using System.Threading;
    using System;
    using global::ServiceControl.LicenseManagement;
    using global::ServiceControl.Persistence;
    using NServiceBus.Logging;

    public class ActiveLicense
    {
        public bool IsValid { get; set; }

        public LicenseDetails Details { get; set; }

        public async Task Refresh(ITrialLicenseMetadataProvider trialLicenseMetadataProvider, CancellationToken cancellationToken)
        {
            Logger.Debug("Refreshing ActiveLicense");

            var detectedLicense = LicenseManager.FindLicense();

            IsValid = !detectedLicense.Details.HasLicenseExpired();

            Details = await ValidateTrialLicense(Details, trialLicenseMetadataProvider, cancellationToken);
        }

        internal static async Task<LicenseDetails> ValidateTrialLicense(LicenseDetails licenseDetails, ITrialLicenseMetadataProvider trialLicenseMetadataProvider, CancellationToken cancellationToken)
        {
            if (licenseDetails.LicenseType.Equals("trial", StringComparison.OrdinalIgnoreCase))
            {
                var trialEndDateInDb = await trialLicenseMetadataProvider.GetTrialEndDate(cancellationToken);
                var trailEndDateInFile = DateOnly.FromDateTime(licenseDetails.ExpirationDate.Value);

                //Trial start has not been stored in the database
                if (trialEndDateInDb == null)
                {
                    await trialLicenseMetadataProvider.StoreTrialEndDate(trailEndDateInFile, cancellationToken);

                    return licenseDetails;
                }

                if (trialEndDateInDb != trailEndDateInFile)
                {
                    //The trial end dates have been tampered. Either in file or in the DB.
                    return LicenseDetails.TrialExpired();
                }
            }
            return licenseDetails;
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ActiveLicense));
    }
}